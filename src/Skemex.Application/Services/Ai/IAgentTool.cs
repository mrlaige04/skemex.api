using System.Text.Json;

namespace Skemex.Application.Services.Ai;

/// <summary>Isolated agent capability that can run directly or via LLM function calling.</summary>
public interface IAgentTool
{
    string SystemName { get; }
    string DefaultDescription { get; }
    string DefaultSystemPrompt { get; }
    object ParameterSchema { get; }

    Task<AiToolExecutionResult> ExecuteDirectAsync(
        JsonElement directArgs,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    Task<AiToolExecutionResult> HandleFunctionCallAsync(
        string argumentsJson,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional project-scoped runtime dataset for prompt injection (e.g. team tables).
    /// Return null/whitespace when the tool has nothing to contribute.
    /// </summary>
    Task<string?> BuildDynamicContextAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>
    /// Side-effect-free check of function-call arguments before execution.
    /// Return null when valid; otherwise a reason suitable for semantic retry logging.
    /// </summary>
    string? ValidateFunctionCallArguments(
        string argumentsJson,
        AgentExecutionContext context)
        => null;
}

public sealed record AiToolExecutionResult(
    bool Success,
    string? AssistantMessage,
    string ArtifactType,
    object? ArtifactPayload,
    string? ErrorMessage = null);

public sealed record AgentExecutionContext
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public Guid? DecompositionJobId { get; init; }
    public Guid? AgentJobId { get; init; }
    public string? Model { get; init; }

    /// <summary>DB-overridden or in-code default system prompt for this tool.</summary>
    public required string EffectiveSystemPrompt { get; init; }

    /// <summary>DB-overridden or in-code default tool description.</summary>
    public required string EffectiveDescription { get; init; }

    /// <summary>
    /// Optional formatted project-document excerpts for RAG. Null/empty means no docs or retrieval skipped.
    /// </summary>
    public string? RagContext { get; init; }

    /// <summary>
    /// Optional tool-specific dynamic runtime context injected after RAG on direct execution.
    /// </summary>
    public string? DynamicToolContext { get; init; }
}
