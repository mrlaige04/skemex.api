using Skemex.Domain.Enums;

namespace Skemex.Application.Models.Ai.Usage;

/// <summary>Immutable payload written by metering interceptors.</summary>
public sealed class AiUsageRecord
{
    /// <summary>Optional pre-assigned id (e.g. parent tool span before nested AI calls).</summary>
    public Guid? ActivityId { get; init; }

    public required AiUsageOperationType OperationType { get; init; }
    public required string OperationName { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? ToolName { get; init; }
    public int? PromptTokens { get; init; }
    public int? CompletionTokens { get; init; }
    public int? TotalTokens { get; init; }
    public int? InputItemCount { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public required long DurationMs { get; init; }
    public string? MetadataJson { get; init; }

    public Guid? CorrelationId { get; init; }
    public Guid? ParentActivityId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? AiChatId { get; init; }
    public Guid? AiChatMessageId { get; init; }
    public Guid? AiAgentJobId { get; init; }
    public Guid? ProjectDocumentId { get; init; }
    public Guid? AiExecutionId { get; init; }
}
