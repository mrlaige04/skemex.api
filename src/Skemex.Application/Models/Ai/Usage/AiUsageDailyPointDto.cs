namespace Skemex.Application.Models.Ai.Usage;

public sealed class AiUsageDailyPointDto
{
    public DateOnly Date { get; init; }
    public long Operations { get; init; }
    public long TotalTokens { get; init; }
    public double AverageLatencyMs { get; init; }
}
