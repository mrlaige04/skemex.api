using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Services.Projects;

public static class ProjectColumnDtoMapper
{
    public static ProjectColumnDto Map(
        ProjectColumn column,
        IReadOnlyDictionary<string, TenantColumn> tenantColumnsByKey)
    {
        tenantColumnsByKey.TryGetValue(column.Key, out var tenantColumn);
        var constraint = TenantColumnConstraints.Resolve(tenantColumn);

        return new ProjectColumnDto
        {
            Id = column.Id,
            Key = column.Key,
            Title = column.Title,
            Description = column.Description,
            SortOrder = column.SortOrder,
            IsRequired = constraint.IsRequired,
            IsSortOrderForced = constraint.IsSortOrderForced,
            ForcedSortOrder = constraint.ForcedSortOrder,
        };
    }
}
