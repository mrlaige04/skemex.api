namespace Skemex.Application.Models.Ai.Usage;

public sealed class AiUsageNamedCountDto
{
    public string Name { get; init; } = string.Empty;
    public long Count { get; init; }
    public long TotalTokens { get; init; }
    public double AverageLatencyMs { get; init; }
}
