namespace Skemex.Application.Models.Ai.Usage;

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
