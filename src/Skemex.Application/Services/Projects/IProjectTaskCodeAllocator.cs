namespace Skemex.Application.Services.Projects;

public interface IProjectTaskCodeAllocator
{
    Task<int> AllocateNextNumberAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

