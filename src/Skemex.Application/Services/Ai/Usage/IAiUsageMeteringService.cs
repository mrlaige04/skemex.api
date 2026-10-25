using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Usage;

/// <summary>Persists AI usage / audit activities. Decoupled from quota enforcement.</summary>
public interface IAiUsageMeteringService
{
    /// <summary>
    /// Records a usage activity using ambient correlation + current user/tenant.
    /// Never throws to callers of AI — failures are logged only.
    /// </summary>
    Task RecordAsync(AiUsageRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a tool execution span and returns its activity id for nesting child ops.
    /// </summary>
    Task<Guid?> RecordToolExecutionAsync(
        AiUsageRecord record,
        CancellationToken cancellationToken = default);
}
