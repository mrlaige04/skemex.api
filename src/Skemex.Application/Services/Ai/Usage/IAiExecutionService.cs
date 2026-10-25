using Skemex.Domain.Entities.Ai;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Usage;

public interface IAiExecutionService
{
    Task<AiExecution> CreatePendingAsync(
        AiExecutionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task MarkRunningAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task CompleteAsync(
        Guid executionId,
        AiExecutionCompleteRequest request,
        CancellationToken cancellationToken = default);
}
