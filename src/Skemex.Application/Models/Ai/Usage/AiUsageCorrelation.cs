namespace Skemex.Application.Models.Ai.Usage;

public sealed record AiUsageCorrelation
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? ProjectId { get; init; }
    public Guid? AiChatId { get; init; }
    public Guid? AiChatMessageId { get; init; }
    public Guid? AiAgentJobId { get; init; }
    public Guid? ProjectDocumentId { get; init; }
    public Guid? AiExecutionId { get; init; }
    public string? OperationName { get; init; }
    public string? ToolName { get; init; }
    public Guid? ParentActivityId { get; init; }
}
