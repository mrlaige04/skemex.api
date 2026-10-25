using ErrorOr;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Usage;

/// <summary>No-op evaluator — always allows. Replace with plan-aware implementation later.</summary>
public sealed class AllowAllAiUsageQuotaEvaluator : IAiUsageQuotaEvaluator
{
    public Task<ErrorOr<Success>> EnsureAllowedAsync(
        AiUsageQuotaCheck check,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<ErrorOr<Success>>(Result.Success);
}
