using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Helpers.Projects;

public static class ProjectColumnDtoHelper
{
    public static ProjectColumnDto Map(
        ProjectColumn column,
        IReadOnlyDictionary<string, TenantColumn> tenantColumnsByKey)
    {
        tenantColumnsByKey.TryGetValue(column.Key, out var tenantColumn);
        var constraint = TenantColumnConstraintsHelper.Resolve(tenantColumn);

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
