using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Models.Rag;
using Skemex.Application.Services.Ai;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AgentOrchestrator(
    IEnumerable<IAgentTool> tools,
    IBaseRepository<AgentTool> toolRepository,
    ITenantRepository<AiAgentJob> jobRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiExecution> executionRepository,
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IProjectRagContextService ragContextService,
    IAiUsageMeteringService usageMetering,
    IAiExecutionLifecycle executionLifecycle,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    private const string IntentRoutingSystemPrompt = """
        You are an intent-routing assistant for an engineering delivery product.
        Analyze the user message and call exactly one registered tool that best matches their intent.
        Populate that tool's arguments strictly according to its JSON schema.
        Copy the user's request into userInput / userQuery without inventing extra instructions or synthetic "additional instructions".
        When a tool schema includes refinedQueryEn and keywords, always fill them for retrieval:
        - refinedQueryEn: a canonical, grammatically normalized technical English search query for the user's goal (even if the user wrote in another language or slang).
        - keywords: 4–8 technical English terms, identifiers, and concepts for exact keyword matching.
        Do not answer the user directly. Do not invent tools. Prefer the most specific matching tool.
        """;

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

        var execution = await executionRepository.GetAsync(
            filter: entry => entry.AiAgentJobId == job.Id,
            cancellationToken: cancellationToken);

        if (execution is null)
        {
            execution = await executionLifecycle.CreatePendingAsync(
                new AiExecutionCreateRequest
                {
                    TenantId = tenantId,
                    UserId = requestedByUserId,
                    ProjectId = job.ProjectId,
                    AiChatId = job.AiChatId,
                    AiAgentJobId = job.Id,
                    Source = job.AiChatId is not null
                        ? AiExecutionSource.Chat
                        : !string.IsNullOrWhiteSpace(job.ToolName)
                            ? AiExecutionSource.Ui
                            : AiExecutionSource.Api,
                    ToolName = job.ToolName,
                    Model = job.Model,
                    UserInput = job.UserInput,
                },
                cancellationToken);
        }

        await executionLifecycle.MarkRunningAsync(execution.Id, cancellationToken);

        using var usageScope = AmbientAiUsageContext.Use(
            correlationId: execution.Id,
            projectId: job.ProjectId,
            aiChatId: job.AiChatId,
            aiAgentJobId: job.Id,
            aiExecutionId: execution.Id,
            toolName: job.ToolName);

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
                await executionLifecycle.CompleteAsync(
                    execution.Id,
                    new AiExecutionCompleteRequest
                    {
                        Success = false,
                        ErrorMessage = job.Error,
                        ToolName = job.ToolName,
                        Model = model,
                    },
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
            await executionLifecycle.CompleteAsync(
                execution.Id,
                new AiExecutionCompleteRequest
                {
                    Success = true,
                    ToolName = job.ToolName,
                    Model = model,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI agent job {JobId} failed.", agentJobId);
            job.Status = AiAgentJobStatus.Failed;
            job.Error = Truncate(ex.Message, 2000);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(job, $"Failed: {job.Error}", cancellationToken);
            await executionLifecycle.CompleteAsync(
                execution.Id,
                new AiExecutionCompleteRequest
                {
                    Success = false,
                    ErrorMessage = job.Error,
                    ToolName = job.ToolName,
                    Model = job.Model,
                },
                cancellationToken);
            throw;
        }
    }

    public async Task<AiToolExecutionResult> ExecuteAsync(
        AgentOrchestratorRequest request,
        CancellationToken cancellationToken = default)
    {
        using var usageScope = EnsureUsageCorrelation(request);

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

    private static IDisposable EnsureUsageCorrelation(AgentOrchestratorRequest request)
    {
        if (AmbientAiUsageContext.Current is not null)
        {
            return NoopDisposable.Instance;
        }

        return AmbientAiUsageContext.Use(
            correlationId: request.AgentJobId ?? Guid.NewGuid(),
            projectId: request.ProjectId,
            aiChatId: request.ChatId,
            aiAgentJobId: request.AgentJobId,
            aiExecutionId: AmbientAiUsageContext.Current?.AiExecutionId,
            toolName: request.ToolName);
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
        var ragRequest = ResolveDirectRagSearchRequest(args, request.UserInput);
        var ragContext = await ResolveRagContextAsync(request.ProjectId, ragRequest, cancellationToken)
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

        return await ExecuteToolWithMeteringAsync(
                tool,
                () => tool.ExecuteDirectAsync(args, context, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
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

        // Stage 1: lightweight intent routing — tools + schemas only (no RAG / dynamic / tool system prompts).
        var definitions = new List<AiToolDefinition>();
        foreach (var tool in toolMap.Values.OrderBy(item => item.SystemName, StringComparer.Ordinal))
        {
            var description = dbByName.TryGetValue(tool.SystemName, out var row)
                && !string.IsNullOrWhiteSpace(row.Description)
                    ? row.Description
                    : tool.DefaultDescription;

            definitions.Add(new AiToolDefinition(tool.SystemName, description, tool.ParameterSchema));
        }

        var stage1UserPrompt = $"## USER REQUEST{Environment.NewLine}{request.UserInput.Trim()}";

        AiFunctionCall call;
        try
        {
            using var stage1Scope = AmbientAiUsageContext.Push(
                operationName: AiUsageOperationNames.Stage1Routing);

            call = await semanticRetry
                .ExecuteAsync(
                    async ct =>
                    {
                        var completion = await aiService.FunctionCallAsync(
                            new AiFunctionCallRequest
                            {
                                SystemPrompt = IntentRoutingSystemPrompt,
                                UserPrompt = stage1UserPrompt,
                                Model = request.Model,
                                Tools = definitions,
                            },
                            ct);

                        if (!completion.HasFunctionCall || completion.FunctionCall is not { } routed)
                        {
                            throw new AiSemanticValidationException(
                                "The model did not return a function call.");
                        }

                        if (!toolMap.ContainsKey(routed.Name))
                        {
                            throw new AiSemanticValidationException(
                                $"Model requested unknown tool '{routed.Name}'.");
                        }

                        var candidate = toolMap[routed.Name];
                        var routingContext = BuildContext(request, candidate, dbByName);
                        var validationError = candidate.ValidateFunctionCallArguments(
                            routed.ArgumentsJson,
                            routingContext);
                        if (!string.IsNullOrWhiteSpace(validationError))
                        {
                            throw new AiSemanticValidationException(
                                $"Tool '{candidate.SystemName}' arguments invalid: {validationError}");
                        }

                        return routed;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AiSemanticValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Stage-1 intent routing retries exhausted for tenant {TenantId}: {Reason}",
                request.TenantId,
                ex.Reason);
            return new AiToolExecutionResult(
                false,
                null,
                "chat",
                null,
                ex.Reason);
        }

        if (!toolMap.TryGetValue(call.Name, out var selectedTool))
        {
            return new AiToolExecutionResult(
                false,
                null,
                "error",
                null,
                $"Model requested unknown tool '{call.Name}'.");
        }

        // Stage 2: selected tool builds RAG / dynamic context and executes.
        var context = BuildContext(request, selectedTool, dbByName);
        logger.LogInformation(
            "Agent Stage-2 execution of {Tool} for tenant {TenantId}",
            selectedTool.SystemName,
            request.TenantId);

        return await ExecuteToolWithMeteringAsync(
                selectedTool,
                () => selectedTool.HandleFunctionCallAsync(call.ArgumentsJson, context, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<AiToolExecutionResult> ExecuteToolWithMeteringAsync(
        IAgentTool tool,
        Func<Task<AiToolExecutionResult>> execute,
        CancellationToken cancellationToken)
    {
        var operationName = ResolveToolOperationName(tool.SystemName);
        var parentActivityId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        AiToolExecutionResult result;
        try
        {
            using var toolScope = AmbientAiUsageContext.Push(
                operationName: operationName,
                toolName: tool.SystemName,
                parentActivityId: parentActivityId);
            result = await execute().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await usageMetering.RecordAsync(
                new AiUsageRecord
                {
                    ActivityId = parentActivityId,
                    OperationType = AiUsageOperationType.ToolExecution,
                    OperationName = AiUsageOperationNames.ToolExecution,
                    ToolName = tool.SystemName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    MetadataJson = $"{{\"toolOperation\":\"{operationName}\"}}",
                },
                cancellationToken).ConfigureAwait(false);
            throw;
        }

        stopwatch.Stop();
        await usageMetering.RecordToolExecutionAsync(
            new AiUsageRecord
            {
                ActivityId = parentActivityId,
                OperationType = AiUsageOperationType.ToolExecution,
                OperationName = AiUsageOperationNames.ToolExecution,
                ToolName = tool.SystemName,
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                DurationMs = stopwatch.ElapsedMilliseconds,
                MetadataJson = $"{{\"toolOperation\":\"{operationName}\",\"artifactType\":\"{result.ArtifactType}\"}}",
            },
            cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static string ResolveToolOperationName(string toolSystemName) =>
        toolSystemName.Trim().ToLowerInvariant() switch
        {
            "task_decomposition" => AiUsageOperationNames.TaskDecomposition,
            "project_qa" => AiUsageOperationNames.ProjectQa,
            _ => toolSystemName.Trim().ToLowerInvariant(),
        };

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
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
                RootTaskId = TryReadRootTaskId(job.ArtifactPayloadJson),
            },
            cancellationToken);
    }

    private static Guid? TryReadRootTaskId(string? artifactPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(artifactPayloadJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(artifactPayloadJson);
            if (doc.RootElement.TryGetProperty("rootTaskId", out var prop)
                && prop.ValueKind == JsonValueKind.String
                && Guid.TryParse(prop.GetString(), out var id))
            {
                return id;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed artifact JSON.
        }

        return null;
    }

    private async Task<string?> ResolveRagContextAsync(
        Guid? projectId,
        RagSearchRequest? request,
        CancellationToken cancellationToken)
    {
        if (projectId is not Guid id || request is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.RefinedQueryEn) && request.Keywords.Count == 0)
        {
            return null;
        }

        return await ragContextService
            .BuildContextAsync(id, request, cancellationToken)
            .ConfigureAwait(false);
    }

    private static RagSearchRequest ResolveDirectRagSearchRequest(
        JsonElement args,
        string fallbackUserInput)
    {
        var refinedQueryEn = ReadString(args, "refinedQueryEn")
            ?? ReadString(args, "userInput")
            ?? ReadString(args, "userQuery")
            ?? ReadString(args, "instructions")
            ?? ReadString(args, "title")
            ?? ReadString(args, "query")
            ?? fallbackUserInput;

        var keywords = ReadStringArray(args, "keywords") ?? [];

        return new RagSearchRequest
        {
            RefinedQueryEn = refinedQueryEn?.Trim() ?? string.Empty,
            Keywords = keywords,
        };
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

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                values.Add(text.Trim());
            }
        }

        return values;
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
            AgentJobId = request.AgentJobId,
            Model = request.Model,
            UserInput = request.UserInput?.Trim() ?? string.Empty,
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
            ["projectId"] = request.ProjectId?.ToString(),
        };

        return JsonSerializer.SerializeToElement(payload);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
