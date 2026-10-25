namespace Skemex.Application.Models.Ai.Agents;

public sealed record AgentExecutionContext
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public Guid? AgentJobId { get; init; }
    public string? Model { get; init; }

    /// <summary>Genuine human user request; never Stage-1 rewrites or synthetic instructions.</summary>
    public required string UserInput { get; init; }

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
