namespace Skemex.Application.Models.Ai.Usage;

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
