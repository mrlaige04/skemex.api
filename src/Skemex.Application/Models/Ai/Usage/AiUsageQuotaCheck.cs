namespace Skemex.Application.Models.Ai.Usage;

public sealed class AiUsageQuotaCheck
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string OperationName { get; init; }
    public string? Model { get; init; }
    public Guid? ProjectId { get; init; }
}
