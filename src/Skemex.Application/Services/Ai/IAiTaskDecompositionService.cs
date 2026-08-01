namespace Skemex.Application.Services.Ai;

/// <summary>
/// Enqueues and processes AI task decomposition jobs.
/// </summary>
public interface IAiTaskDecompositionService
{
    string Enqueue(Guid jobId, Guid tenantId, Guid requestedByUserId);

    Task ProcessJobAsync(
        Guid jobId,
        Guid tenantId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
