using ErrorOr;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

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
