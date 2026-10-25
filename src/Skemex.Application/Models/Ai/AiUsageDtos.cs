namespace Skemex.Application.Models.Ai;

public sealed class AiUsageActivityDto
{
    public Guid Id { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid? ParentActivityId { get; init; }
    public string OperationType { get; init; } = string.Empty;
    public string OperationName { get; init; } = string.Empty;
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? ToolName { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
    public int? InputItemCount { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public long DurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid UserId { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public string? UserEmail { get; init; }
}

public sealed class ProjectAiUsageSummaryDto
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public long TotalOperations { get; init; }
    public long SuccessfulOperations { get; init; }
    public double SuccessRate { get; init; }
    public long TotalTokens { get; init; }
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public double AverageLatencyMs { get; init; }
    public long EmbeddingOperations { get; init; }
    public long ToolExecutions { get; init; }
    public long ChatCompletions { get; init; }
    public long FunctionCalls { get; init; }
    public IReadOnlyList<AiUsageNamedCountDto> ByOperationType { get; init; } = [];
    public IReadOnlyList<AiUsageNamedCountDto> ByOperationName { get; init; } = [];
    public IReadOnlyList<AiUsageNamedCountDto> ByModel { get; init; } = [];
    public IReadOnlyList<AiUsageDailyPointDto> Daily { get; init; } = [];
}

public sealed class AiUsageNamedCountDto
{
    public string Name { get; init; } = string.Empty;
    public long Count { get; init; }
    public long TotalTokens { get; init; }
    public double AverageLatencyMs { get; init; }
}

public sealed class AiUsageDailyPointDto
{
    public DateOnly Date { get; init; }
    public long Operations { get; init; }
    public long TotalTokens { get; init; }
    public double AverageLatencyMs { get; init; }
}

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
