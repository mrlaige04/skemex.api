namespace Skemex.Domain.Enums;

/// <summary>High-level AI / metering operation categories for dashboards and quotas.</summary>
public enum AiUsageOperationType
{
    ChatCompletion = 0,
    FunctionCall = 1,
    Embedding = 2,
    ToolExecution = 3,
}
