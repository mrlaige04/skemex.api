using ErrorOr;

namespace Skemex.Application.Services.Ai.Usage;

/// <summary>
/// Architectural hook for future subscription quotas / billing limits.
/// Tracking remains independent — this gate runs before the provider call.
/// </summary>
public interface IAiUsageQuotaEvaluator
{
    Task<ErrorOr<Success>> EnsureAllowedAsync(
        AiUsageQuotaCheck check,
        CancellationToken cancellationToken = default);
}

public sealed class AiUsageQuotaCheck
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string OperationName { get; init; }
    public string? Model { get; init; }
    public Guid? ProjectId { get; init; }
}

/// <summary>No-op evaluator — always allows. Replace with plan-aware implementation later.</summary>
public sealed class AllowAllAiUsageQuotaEvaluator : IAiUsageQuotaEvaluator
{
    public Task<ErrorOr<Success>> EnsureAllowedAsync(
        AiUsageQuotaCheck check,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ErrorOr<Success>>(Result.Success);
}
