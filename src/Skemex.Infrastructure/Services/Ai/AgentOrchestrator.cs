using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AgentOrchestrator(
    IEnumerable<IAgentTool> tools,
    IBaseRepository<AgentTool> toolRepository,
    ITenantRepository<AiAgentJob> jobRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<AiChat> chatRepository,
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IProjectRagContextService ragContextService,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    private const string ChatSystemPrompt = """
        You are a Principal Software Architect and Technical Delivery Lead assisting an engineering team.
        
        Your responsibilities:
        1. Technical Advisory: Provide crisp, production-grade technical guidance on architecture, engineering trade-offs, system design, and implementation details.
        2. Backlog & Scope Analysis: When asked to analyze, break down, or plan product goals, technical specifications, or user stories, always utilize the provided specialized tools rather than replying with raw unstructured text.
        3. Documentation Authority: Whenever project documentation context is provided (via knowledge base / RAG), treat it as the absolute source of truth for business domain logic, naming conventions, technical constraints, and architectural boundaries. Never contradict project documentation.
        4. Professional Style: Communicate concisely, factually, and pragmatically. Avoid generic conversational filler, sycophancy, or vague corporate speak. Focus on actionable outcomes, technical rigor, and delivery risk mitigation.
        """;

    // Keep in sync with TaskDecompositionTool prompt limits (chat path substitutes the same tokens).
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 50000;
    private const int MaxAcceptanceCriterionLength = 500;
    private const int MaxRisks = 8;
    private const int MaxRiskLength = 500;
    private const int MaxTestCaseFieldLength = 500;

    public string Enqueue(Guid agentJobId, Guid tenantId, Guid requestedByUserId) =>
        BackgroundJob.Enqueue<AgentOrchestrator>(orchestrator =>
            orchestrator.ProcessJobAsync(agentJobId, tenantId, requestedByUserId, CancellationToken.None));

    [AutomaticRetry(Attempts = 2)]
    public async Task ProcessJobAsync(
        Guid agentJobId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        using var ambient = AmbientUserContext.Use(tenantId, requestedByUserId);

        var job = await jobRepository.GetAsync(
            filter: entry => entry.Id == agentJobId,
            cancellationToken: cancellationToken);

        if (job is null)
        {
            logger.LogWarning("AI agent job {JobId} was not found.", agentJobId);
            return;
        }

        if (job.Status is AiAgentJobStatus.Succeeded or AiAgentJobStatus.Failed)
        {
            return;
        }

        job.Status = AiAgentJobStatus.Running;
        job.Error = null;
        await jobRepository.UpdateAsync(job, cancellationToken);

        try
        {
            var model = job.Model;
            if (string.IsNullOrWhiteSpace(model) && job.ProjectId is { } projectId)
            {
                var settings = await projectSettingsRepository.GetAsync(
                    filter: entry => entry.ProjectId == projectId,
                    include: query => query.Include(entry => entry.DefaultAiModel),
                    cancellationToken: cancellationToken);
                model = settings?.DefaultAiModel?.ExternalId;

                if (string.IsNullOrWhiteSpace(model) && job.AiChatId is { } chatId)
                {
                    var chat = await chatRepository.GetAsync(
                        filter: entry => entry.Id == chatId,
                        include: query => query.Include(entry => entry.AiModel),
                        cancellationToken: cancellationToken);
                    model = chat?.AiModel?.ExternalId;
                }
            }

            var result = await ExecuteAsync(
                new AgentOrchestratorRequest
                {
                    TenantId = tenantId,
                    UserId = requestedByUserId,
                    ProjectId = job.ProjectId,
                    ChatId = job.AiChatId,
                    AgentJobId = job.Id,
                    ToolName = job.ToolName,
                    UserInput = job.UserInput,
                    CustomInstructions = job.CustomInstructions,
                    Model = model,
                    ArgumentsJson = job.ArgumentsJson,
                },
                cancellationToken);

            if (!result.Success)
            {
                job.Status = AiAgentJobStatus.Failed;
                job.Error = Truncate(result.ErrorMessage ?? "Agent execution failed.", 2000);
                job.AssistantMessage = result.AssistantMessage;
                await jobRepository.UpdateAsync(job, cancellationToken);
                await AppendAssistantMessageAsync(
                    job,
                    result.AssistantMessage ?? $"Failed: {job.Error}",
                    cancellationToken);
                return;
            }

            job.Status = AiAgentJobStatus.Succeeded;
            job.Error = null;
            job.AssistantMessage = result.AssistantMessage;
            job.ArtifactType = result.ArtifactType;
            job.ArtifactPayloadJson = result.ArtifactPayload is null
                ? null
                : JsonSerializer.Serialize(result.ArtifactPayload);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                result.AssistantMessage ?? "Done.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI agent job {JobId} failed.", agentJobId);
            job.Status = AiAgentJobStatus.Failed;
            job.Error = Truncate(ex.Message, 2000);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(job, $"Failed: {job.Error}", cancellationToken);
            throw;
        }
    }

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AgentOrchestratorRequest request,
        CancellationToken cancellationToken = default)
    {
        var toolMap = tools.ToDictionary(tool => tool.SystemName, StringComparer.OrdinalIgnoreCase);
        var dbTools = await toolRepository.GetAllAsync(cancellationToken: cancellationToken);
        var dbByName = dbTools.ToDictionary(tool => tool.SystemName, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(request.ToolName))
        {
            return await ExecuteDirectToolAsync(request, toolMap, dbByName, cancellationToken)
                .ConfigureAwait(false);
        }

        return await ExecuteChatWithToolsAsync(request, toolMap, dbByName, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AiToolExecutionResult> ExecuteDirectToolAsync(
        AgentOrchestratorRequest request,
        IReadOnlyDictionary<string, IAgentTool> toolMap,
        IReadOnlyDictionary<string, AgentTool> dbByName,
        CancellationToken cancellationToken)
    {
        var toolName = request.ToolName!.Trim();
        if (!toolMap.TryGetValue(toolName, out var tool))
        {
            return new AiToolExecutionResult(
                false,
                null,
                "error",
                null,
                $"Unknown tool '{toolName}'.");
        }

        var context = BuildContext(request, tool, dbByName);
        var args = BuildDirectArgs(request);
        var ragQuery = ResolveDirectRagQuery(args, request.UserInput);
        var ragContext = await ResolveRagContextAsync(request.ProjectId, ragQuery, cancellationToken)
            .ConfigureAwait(false);
        var dynamicContext = await tool.BuildDynamicContextAsync(context, cancellationToken)
            .ConfigureAwait(false);
        context = context with
        {
            RagContext = ragContext,
            DynamicToolContext = dynamicContext,
        };

        logger.LogInformation(
            "Agent direct execution of {Tool} for tenant {TenantId}",
            tool.SystemName,
            request.TenantId);

        return await tool.ExecuteDirectAsync(args, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AiToolExecutionResult> ExecuteChatWithToolsAsync(
        AgentOrchestratorRequest request,
        IReadOnlyDictionary<string, IAgentTool> toolMap,
        IReadOnlyDictionary<string, AgentTool> dbByName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return new AiToolExecutionResult(
                false,
                null,
                "chat",
                null,
                "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.");
        }

        var definitions = new List<AiToolDefinition>();
        foreach (var tool in toolMap.Values.OrderBy(item => item.SystemName, StringComparer.Ordinal))
        {
            var description = dbByName.TryGetValue(tool.SystemName, out var row)
                && !string.IsNullOrWhiteSpace(row.Description)
                    ? row.Description
                    : tool.DefaultDescription;

            definitions.Add(new AiToolDefinition(tool.SystemName, description, tool.ParameterSchema));
        }

        var systemPrompt = await BuildCombinedSystemPromptAsync(
                toolMap,
                dbByName,
                request.ProjectId,
                cancellationToken)
            .ConfigureAwait(false);

        var userGoal = string.IsNullOrWhiteSpace(request.CustomInstructions)
            ? request.UserInput
            : $"{request.UserInput}{Environment.NewLine}{Environment.NewLine}Additional instructions:{Environment.NewLine}{request.CustomInstructions}";

        var ragContext = await ResolveRagContextAsync(request.ProjectId, request.UserInput, cancellationToken)
            .ConfigureAwait(false);

        var toolContextBlocks = new List<(string ToolName, string Content)>();
        foreach (var tool in toolMap.Values.OrderBy(item => item.SystemName, StringComparer.Ordinal))
        {
            var toolContext = BuildContext(request, tool, dbByName);
            var dynamicContext = await tool.BuildDynamicContextAsync(toolContext, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(dynamicContext))
            {
                continue;
            }

            toolContextBlocks.Add((tool.SystemName, dynamicContext.Trim()));
        }

        var userPrompt = AssembleChatUserPrompt(ragContext, toolContextBlocks, userGoal);

        try
        {
            return await semanticRetry
                .ExecuteAsync(
                    async ct =>
                    {
                        var completion = await aiService.FunctionCallAsync(
                            new AiFunctionCallRequest
                            {
                                SystemPrompt = systemPrompt,
                                UserPrompt = userPrompt,
                                Model = request.Model,
                                Tools = definitions,
                            },
                            ct);

                        if (!completion.HasFunctionCall || completion.FunctionCall is not { } call)
                        {
                            throw new AiSemanticValidationException(
                                "The model did not return a function call.");
                        }

                        if (!toolMap.TryGetValue(call.Name, out var tool))
                        {
                            // Unknown tool name is not a parse failure — do not retry.
                            return new AiToolExecutionResult(
                                false,
                                null,
                                "error",
                                null,
                                $"Model requested unknown tool '{call.Name}'.");
                        }

                        var context = BuildContext(request, tool, dbByName) with { RagContext = ragContext };
                        var validationError = tool.ValidateFunctionCallArguments(
                            call.ArgumentsJson,
                            context);
                        if (!string.IsNullOrWhiteSpace(validationError))
                        {
                            throw new AiSemanticValidationException(
                                $"Tool '{tool.SystemName}' arguments invalid: {validationError}");
                        }

                        logger.LogInformation(
                            "Agent function call {Tool} for tenant {TenantId}",
                            tool.SystemName,
                            request.TenantId);

                        return await tool.HandleFunctionCallAsync(call.ArgumentsJson, context, ct)
                            .ConfigureAwait(false);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AiSemanticValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Chat semantic retries exhausted for tenant {TenantId}: {Reason}",
                request.TenantId,
                ex.Reason);
            return new AiToolExecutionResult(
                false,
                null,
                "chat",
                null,
                ex.Reason);
        }
    }

    private async Task AppendAssistantMessageAsync(
        AiAgentJob job,
        string content,
        CancellationToken cancellationToken)
    {
        if (job.AiChatId is null)
        {
            return;
        }

        await messageRepository.AddAsync(
            new AiChatMessage
            {
                Id = Guid.NewGuid(),
                TenantId = job.TenantId,
                ChatId = job.AiChatId.Value,
                Role = AiChatMessageRole.Assistant,
                Content = content,
            },
            cancellationToken);
    }

    private async Task<string> BuildCombinedSystemPromptAsync(
        IReadOnlyDictionary<string, IAgentTool> toolMap,
        IReadOnlyDictionary<string, AgentTool> dbByName,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var maxDepth = 2;
        var maxNodes = 16;
        if (projectId is Guid pid)
        {
            var settings = await projectSettingsRepository.GetAsync(
                filter: entry => entry.ProjectId == pid,
                cancellationToken: cancellationToken);
            if (settings is not null)
            {
                maxDepth = Math.Clamp(settings.AiMaxTreeDepth, 1, 8);
                maxNodes = Math.Clamp(settings.AiMaxNodes, 1, 64);
            }
        }

        var parts = new List<string>
        {
            "## Main system prompt",
            ChatSystemPrompt.Trim(),
        };

        foreach (var tool in toolMap.Values.OrderBy(item => item.SystemName, StringComparer.Ordinal))
        {
            var toolPrompt = tool.DefaultSystemPrompt;
            if (dbByName.TryGetValue(tool.SystemName, out var row)
                && !string.IsNullOrWhiteSpace(row.SystemPrompt))
            {
                toolPrompt = row.SystemPrompt;
            }

            if (string.IsNullOrWhiteSpace(toolPrompt))
            {
                continue;
            }

            parts.Add(string.Empty);
            parts.Add($"## Tool system prompt: {tool.SystemName}");
            parts.Add(
                "When calling this tool, follow these rules for the tool arguments:");
            parts.Add(ApplyToolPromptPlaceholders(toolPrompt.Trim(), maxDepth, maxNodes));
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string ApplyToolPromptPlaceholders(string template, int maxDepth, int maxNodes)
    {
        var maxChildrenHint = Math.Max(1, maxNodes - 1);
        var maxEstimateHours = ProjectTaskTimeTracking.MaxEstimateHours;

        return template
            .Replace("__MAX_DEPTH__", maxDepth.ToString())
            .Replace("__MAX_CHILDREN__", maxChildrenHint.ToString())
            .Replace("__MAX_NODES__", maxNodes.ToString())
            .Replace("__MAX_TITLE__", MaxTitleLength.ToString())
            .Replace("__MAX_DESCRIPTION__", MaxDescriptionLength.ToString())
            .Replace("__MAX_AC_ITEM__", MaxAcceptanceCriterionLength.ToString())
            .Replace("__MAX_RISKS__", MaxRisks.ToString())
            .Replace("__MAX_RISK_ITEM__", MaxRiskLength.ToString())
            .Replace("__MAX_TEST_FIELD__", MaxTestCaseFieldLength.ToString())
            .Replace("__MAX_ESTIMATE_HOURS__", maxEstimateHours.ToString());
    }

    private async Task<string?> ResolveRagContextAsync(
        Guid? projectId,
        string? query,
        CancellationToken cancellationToken)
    {
        if (projectId is not Guid id || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return await ragContextService
            .BuildContextAsync(id, query, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string AssembleChatUserPrompt(
        string? ragContext,
        IReadOnlyList<(string ToolName, string Content)> toolContextBlocks,
        string userGoal)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(ragContext))
        {
            parts.Add(ragContext.Trim());
        }

        if (toolContextBlocks.Count > 0)
        {
            parts.Add("## TOOL EXECUTION CONTEXTS");
            parts.Add("The following datasets provide dynamic runtime context strictly intended for evaluating arguments of specific tools:");
            parts.Add(string.Empty);

            foreach (var (toolName, content) in toolContextBlocks)
            {
                parts.Add($"### Context for tool: {toolName}");
                parts.Add(content);
                parts.Add(string.Empty);
            }
        }

        parts.Add("## USER PROMPT");
        parts.Add(userGoal.Trim());
        return string.Join(Environment.NewLine, parts);
    }

    private static string ResolveDirectRagQuery(JsonElement args, string fallbackUserInput)
    {
        var fromArgs = ReadString(args, "userInput")
            ?? ReadString(args, "instructions")
            ?? ReadString(args, "title")
            ?? ReadString(args, "query");
        if (!string.IsNullOrWhiteSpace(fromArgs))
        {
            return fromArgs.Trim();
        }

        return fallbackUserInput?.Trim() ?? string.Empty;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static AgentExecutionContext BuildContext(
        AgentOrchestratorRequest request,
        IAgentTool tool,
        IReadOnlyDictionary<string, AgentTool> dbByName)
    {
        var description = tool.DefaultDescription;
        var systemPrompt = tool.DefaultSystemPrompt;
        if (dbByName.TryGetValue(tool.SystemName, out var row))
        {
            if (!string.IsNullOrWhiteSpace(row.Description))
            {
                description = row.Description;
            }

            if (!string.IsNullOrWhiteSpace(row.SystemPrompt))
            {
                systemPrompt = row.SystemPrompt;
            }
        }

        return new AgentExecutionContext
        {
            TenantId = request.TenantId,
            UserId = request.UserId,
            ProjectId = request.ProjectId,
            ChatId = request.ChatId,
            DecompositionJobId = request.DecompositionJobId,
            AgentJobId = request.AgentJobId,
            Model = request.Model,
            EffectiveDescription = description,
            EffectiveSystemPrompt = systemPrompt,
        };
    }

    private static JsonElement BuildDirectArgs(AgentOrchestratorRequest request)
    {
        if (request.DirectArgs is { } provided
            && provided.ValueKind is JsonValueKind.Object)
        {
            return provided;
        }

        if (!string.IsNullOrWhiteSpace(request.ArgumentsJson))
        {
            using var parsed = JsonDocument.Parse(request.ArgumentsJson);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object)
            {
                return parsed.RootElement.Clone();
            }
        }

        var payload = new Dictionary<string, object?>
        {
            ["userInput"] = request.UserInput,
            ["customInstructions"] = request.CustomInstructions,
            ["projectId"] = request.ProjectId?.ToString(),
        };

        return JsonSerializer.SerializeToElement(payload);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
