namespace Skemex.Application.Models.Ai.Usage;

/// <summary>
/// One top-level AI execution (<c>ai_executions</c>) with nested metered steps.
/// </summary>
public sealed class AiUsageGroupDto
{
    public Guid Id { get; init; }

    /// <summary>Same as <see cref="Id"/>; kept for older clients.</summary>
    public Guid CorrelationId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string ContextLabel { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public int OperationCount { get; init; }
    public int ToolCount { get; init; }
    public long TotalTokens { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public long DurationMs { get; init; }
    public bool Success { get; init; }
    public Guid UserId { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public string? UserEmail { get; init; }
    public IReadOnlyList<string> OperationTypes { get; init; } = [];
    public IReadOnlyList<string> ToolNames { get; init; } = [];
    public IReadOnlyList<AiUsageActivityDto> Activities { get; init; } = [];
}
