using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;
using Skemex.Application.Models.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Events.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Application.Helpers.Ai;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Domain.Consts;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AgentOrchestrator(
    IEnumerable<IAgentTool> tools,
    IBaseRepository<AgentTool> toolRepository,
    ITenantRepository<AiAgentJob> jobRepository,
    ITenantRepository<AiChatMessage> messageRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<AiChat> chatRepository,
    ITenantRepository<AiExecution> executionRepository,
    ITenantRepository<Project> projectRepository,
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IProjectRagContextService ragContextService,
    IAiUsageMeteringService usageMetering,
    IAiExecutionService executionService,
    IAiChatProgressWriter progressWriter,
    IAiChatAttachmentStore attachmentStore,
    IAiChatArtefactResolver artefactResolver,
    ILogger<AgentOrchestrator> logger) : IAgentOrchestrator
{
    private static readonly JsonSerializerOptions AttachmentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    
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
            execution = await executionService.CreatePendingAsync(
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

        await executionService.MarkRunningAsync(execution.Id, cancellationToken);

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
        await progressWriter.WriteAsync(
            tenantId,
            job.AiChatId,
            "Working on your request…",
            cancellationToken);

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

            var attachmentRefs = ParseAttachmentRefs(job.AttachmentsJson);
            var completionAttachments = attachmentRefs.Count == 0
                ? new List<AiCompletionAttachment>()
                : (await attachmentStore
                    .LoadForCompletionAsync(attachmentRefs, cancellationToken)
                    .ConfigureAwait(false)).ToList();

            var artefactRefs = ParseArtefactRefs(job.ArtefactsJson);
            string? artefactContext = null;
            if (artefactRefs.Count > 0 && job.ProjectId is Guid artefactProjectId)
            {
                var resolved = await artefactResolver
                    .ResolveAsync(tenantId, artefactProjectId, artefactRefs, cancellationToken)
                    .ConfigureAwait(false);
                artefactContext = resolved.PromptTableMarkdown;
                if (resolved.DocumentAttachments.Count > 0)
                {
                    completionAttachments.AddRange(resolved.DocumentAttachments);
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
                    AttachmentRefs = attachmentRefs,
                    CompletionAttachments = completionAttachments,
                    ArtefactRefs = artefactRefs,
                    ArtefactContext = artefactContext,
                },
                cancellationToken);

            if (!result.Success)
            {
                var friendlyError = ResolveFriendlyFailure(result);
                job.Status = AiAgentJobStatus.Failed;
                job.Error = Truncate(friendlyError, 2000);
                job.AssistantMessage = friendlyError;
                await RaiseExecutionFinishedAsync(
                    job,
                    execution.Id,
                    AiExecutionStatus.Failed,
                    artifactType: result.ArtifactType,
                    cancellationToken);
                await jobRepository.UpdateAsync(job, cancellationToken);
                await AppendAssistantMessageAsync(job, friendlyError, cancellationToken);
                await executionService.CompleteAsync(
                    execution.Id,
                    new AiExecutionCompleteRequest
                    {
                        Success = false,
                        ErrorMessage = Truncate(result.ErrorMessage ?? friendlyError, 2000),
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
            await RaiseExecutionFinishedAsync(
                job,
                execution.Id,
                AiExecutionStatus.Succeeded,
                artifactType: result.ArtifactType,
                cancellationToken);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(
                job,
                result.AssistantMessage ?? "Done.",
                cancellationToken);
            await executionService.CompleteAsync(
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
            var friendlyError = AiUserFacingError.FromException(ex);
            job.Status = AiAgentJobStatus.Failed;
            job.Error = Truncate(friendlyError, 2000);
            job.AssistantMessage = friendlyError;
            await RaiseExecutionFinishedAsync(
                job,
                execution.Id,
                AiExecutionStatus.Failed,
                artifactType: job.ArtifactType,
                cancellationToken);
            await jobRepository.UpdateAsync(job, cancellationToken);
            await AppendAssistantMessageAsync(job, friendlyError, cancellationToken);
            await executionService.CompleteAsync(
                execution.Id,
                new AiExecutionCompleteRequest
                {
                    Success = false,
                    ErrorMessage = Truncate(ex.Message, 2000),
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
                AiUserFacingError.RoutingFailed,
                "error",
                null,
                AiUserFacingError.RoutingFailed);
        }

        var context = BuildContext(request, tool, dbByName);
        var args = BuildDirectArgs(request);
        var ragRequest = ResolveDirectRagSearchRequest(
            args,
            request.UserInput,
            request.ArtefactRefs);

        await progressWriter.WriteAsync(
            request.TenantId,
            request.ChatId,
            "Gathering related project context…",
            cancellationToken).ConfigureAwait(false);

        var ragContext = await ResolveRagContextAsync(request.ProjectId, ragRequest, cancellationToken)
            .ConfigureAwait(false);
        var dynamicContext = await tool.BuildDynamicContextAsync(context, cancellationToken)
            .ConfigureAwait(false);
        context = context with
        {
            RagContext = ragContext,
            DynamicToolContext = dynamicContext,
        };

        await progressWriter.WriteAsync(
            request.TenantId,
            request.ChatId,
            ResolveToolStartMessage(tool.SystemName),
            cancellationToken).ConfigureAwait(false);

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
                AiUserFacingError.NoModel,
                "chat",
                null,
                AiUserFacingError.NoModel);
        }

        await progressWriter.WriteAsync(
            request.TenantId,
            request.ChatId,
            "Choosing the best approach…",
            cancellationToken).ConfigureAwait(false);

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

        var stage1UserPrompt = BuildStage1UserPrompt(request);

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
            var friendly = AiUserFacingError.FromSemanticReason(ex.Reason);
            return new AiToolExecutionResult(
                false,
                friendly,
                "chat",
                null,
                friendly);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Stage-1 intent routing failed for tenant {TenantId}",
                request.TenantId);
            var friendly = AiUserFacingError.FromException(ex);
            return new AiToolExecutionResult(
                false,
                friendly,
                "chat",
                null,
                friendly);
        }

        if (!toolMap.TryGetValue(call.Name, out var selectedTool))
        {
            return new AiToolExecutionResult(
                false,
                AiUserFacingError.RoutingFailed,
                "error",
                null,
                AiUserFacingError.RoutingFailed);
        }

        await progressWriter.WriteAsync(
            request.TenantId,
            request.ChatId,
            ResolveToolStartMessage(selectedTool.SystemName),
            cancellationToken).ConfigureAwait(false);

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

    private static string ResolveToolStartMessage(string toolSystemName) =>
        toolSystemName.Trim().ToLowerInvariant() switch
        {
            "task_decomposition" => "Starting task breakdown…",
            "project_qa" => "Looking up an answer in this project…",
            _ => "Running the next step…",
        };

    private static string ResolveFriendlyFailure(AiToolExecutionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.AssistantMessage)
            && !LooksTechnical(result.AssistantMessage))
        {
            return result.AssistantMessage.Trim();
        }

        return AiUserFacingError.FromMessage(result.ErrorMessage ?? result.AssistantMessage);
    }

    private static bool LooksTechnical(string message)
    {
        var text = message.Trim();
        return text.Contains("Exception", StringComparison.OrdinalIgnoreCase)
            || text.Contains("HTTP ", StringComparison.OrdinalIgnoreCase)
            || text.Contains("status code", StringComparison.OrdinalIgnoreCase)
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[1-5]\d{2}\b");
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose()
        {
        }
    }

    private async Task RaiseExecutionFinishedAsync(
        AiAgentJob job,
        Guid aiExecutionId,
        AiExecutionStatus status,
        string? artifactType,
        CancellationToken cancellationToken)
    {
        var projectCode = await ResolveProjectCodeAsync(job.ProjectId, cancellationToken);
        job.RaiseEvent(
            new AiExecutionFinishedDomainEvent
            {
                AiExecutionId = aiExecutionId,
                AiAgentJobId = job.Id,
                UserId = job.RequestedByUserId,
                TenantId = job.TenantId,
                ProjectId = job.ProjectId,
                ProjectCode = projectCode,
                Status = status,
                ToolName = job.ToolName,
                ArtifactType = artifactType ?? job.ArtifactType,
                AssistantMessage = job.AssistantMessage,
                UserInput = job.UserInput,
                Error = job.Error,
            });
    }

    private async Task<string?> ResolveProjectCodeAsync(
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        if (projectId is not Guid id)
        {
            return null;
        }

        var project = await projectRepository.GetAsync(
            filter: entry => entry.Id == id,
            cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(project?.Code) ? null : project.Code;
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
        string fallbackUserInput,
        IReadOnlyList<AiChatArtefactRef>? artefactRefs)
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
            // Document artefacts are attached in full — do not also retrieve their RAG chunks.
            ExcludeDocumentIds = artefactRefs.GetDocumentIds(),
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
            AttachmentRefs = request.AttachmentRefs,
            CompletionAttachments = request.CompletionAttachments,
            ArtefactRefs = request.ArtefactRefs,
            ArtefactContext = request.ArtefactContext,
        };
    }

    private static IReadOnlyList<AiChatArtefactRef> ParseArtefactRefs(string? artefactsJson)
    {
        if (string.IsNullOrWhiteSpace(artefactsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AiChatArtefactRef>>(
                       artefactsJson,
                       AttachmentJsonOptions)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<AiChatAttachmentRef> ParseAttachmentRefs(string? attachmentsJson)
    {
        if (string.IsNullOrWhiteSpace(attachmentsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AiChatAttachmentRef>>(
                       attachmentsJson,
                       AttachmentJsonOptions)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildStage1UserPrompt(AgentOrchestratorRequest request)
    {
        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.ArtefactContext))
        {
            builder.AppendLine("## ARTEFACTS");
            builder.AppendLine(request.ArtefactContext.Trim());
            builder.AppendLine();
        }

        builder.AppendLine("## USER REQUEST");
        builder.Append(request.UserInput.Trim());

        var fileNames = request.AttachmentRefs
            .Where(item => string.Equals(item.Kind, AiChatAttachmentKinds.File, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.FileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (fileNames.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine();
            builder.Append("## ATTACHED FILES");
            builder.AppendLine();
            foreach (var name in fileNames)
            {
                builder.Append("- ").AppendLine(name);
            }
        }

        return builder.ToString().TrimEnd();
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
