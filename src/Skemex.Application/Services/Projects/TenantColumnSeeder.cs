using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Projects;

public sealed class TenantColumnSeeder(IBaseRepository<TenantColumn> tenantColumnRepository)
{
    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var hasColumns = await tenantColumnRepository.ExistsAsync(
            filter: column => column.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (hasColumns)
        {
            return;
        }

        var columns = TenantColumnDefaults.KanbanColumns
            .Select((definition, index) => new TenantColumn
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = definition.Key,
                Title = definition.Title,
                SortOrder = index,
                IsRequired = false,
                IsSortOrderForced = false,
            })
            .ToList();

        await tenantColumnRepository.AddRangeAsync(columns, cancellationToken);
    }
}
