using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Projects;

public sealed class ProjectColumnSeeder(ITenantRepository<TenantColumn> tenantColumnRepository)
{
    public async Task<IReadOnlyList<ProjectColumn>> CreateForProjectAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var tenantColumns = await tenantColumnRepository.GetAllAsync(
            filter: column => column.TenantId == tenantId,
            include: query => query.OrderBy(column => column.SortOrder).ThenBy(column => column.Title),
            cancellationToken: cancellationToken);

        return tenantColumns
            .Select(tenantColumn => new ProjectColumn
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                Key = tenantColumn.Key,
                Title = tenantColumn.Title,
                Description = tenantColumn.Description,
                SortOrder = tenantColumn.SortOrder,
            })
            .ToList();
    }
}
