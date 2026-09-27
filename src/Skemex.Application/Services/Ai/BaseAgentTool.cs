using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Ai;

/// <summary>
/// Shared agent-tool lifecycle: project/model resolution and <see cref="HandleFunctionCallAsync"/> prep,
/// then delegates to <see cref="ExecuteInternalAsync"/>. Tool-agnostic — no domain prompt or WBS logic.
/// </summary>
public abstract class BaseAgentTool(
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    IProjectRagContextService ragContextService,
    IAiService aiService,
    ILogger logger) : IAgentTool
{
    private static readonly JsonSerializerOptions OutputSchemaSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    protected ITenantRepository<ProjectSettings> ProjectSettingsRepository { get; } = projectSettingsRepository;
    protected IProjectRagContextService RagContextService { get; } = ragContextService;
    protected IAiService AiService { get; } = aiService;
    protected ILogger Logger { get; } = logger;

    public abstract string SystemName { get; }
    public abstract string DefaultDescription { get; }
    public abstract string DefaultSystemPrompt { get; }
    public abstract object ParameterSchema { get; }

    /// <inheritdoc />
    public virtual object? OutputSchema => null;

    public abstract Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    public virtual Task<string?> BuildDynamicContextAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public virtual string? ValidateFunctionCallArguments(
        string argumentsJson,
        AgentExecutionContext context)
        => null;

    public async Task<AiToolExecutionResult> HandleFunctionCallAsync(
        string argumentsJson,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.ProjectId is not Guid projectId)
        {
            return Fail("ProjectId is required.");
        }

        var settings = await ProjectSettingsRepository.GetAsync(
                filter: entry => entry.ProjectId == projectId,
                include: query => query.Include(entry => entry.DefaultAiModel),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            return Fail("Project settings were not found.");
        }

        var modelExternalId = await ResolveModelExternalIdAsync(context, settings, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(modelExternalId))
        {
            return Fail(
                "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.");
        }

        var prepared = new ToolPreparedContext
        {
            ProjectId = projectId,
            Settings = settings,
            ModelExternalId = modelExternalId.Trim(),
            ArgumentsJson = argumentsJson ?? string.Empty,
        };

        return await ExecuteInternalAsync(prepared, context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tool-specific Stage-2 execution after shared prep.</summary>
    protected abstract Task<AiToolExecutionResult> ExecuteInternalAsync(
        ToolPreparedContext preparedContext,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Serializes <see cref="OutputSchema"/> into a structured instruction block for the Stage-2 model.
    /// Returns empty when no output schema is defined.
    /// </summary>
    protected virtual string BuildOutputSchemaInstructionBlock()
    {
        if (OutputSchema is null)
        {
            return string.Empty;
        }

        var serialized = JsonSerializer.Serialize(OutputSchema, OutputSchemaSerializerOptions);
        return
            "### MANDATORY JSON OUTPUT SCHEMA CONTRACT:" + Environment.NewLine +
            "Your response MUST strictly adhere to the JSON schema defined below. " +
            "Do not wrap the JSON in markdown code blocks or backticks. " +
            "Return exclusively a single valid JSON payload conforming to this schema:" + Environment.NewLine +
            "<schema_definition>" + Environment.NewLine +
            serialized + Environment.NewLine +
            "</schema_definition>";
    }

    protected virtual Task<string?> ResolveModelExternalIdAsync(
        AgentExecutionContext context,
        ProjectSettings settings,
        CancellationToken cancellationToken)
        => Task.FromResult(ResolveModelExternalId(context, settings));

    protected virtual string? ResolveModelExternalId(
        AgentExecutionContext context,
        ProjectSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(context.Model))
        {
            return context.Model.Trim();
        }

        return settings.DefaultAiModel?.ExternalId;
    }

    protected virtual AiToolExecutionResult Fail(string error) =>
        new(false, null, "error", null, error);
}
