using ErrorOr;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Services.Projects;

public sealed record TenantColumnConstraint(
    bool IsRequired,
    bool IsSortOrderForced,
    int? ForcedSortOrder);

public static class TenantColumnConstraints
{
    public static TenantColumnConstraint Resolve(TenantColumn? tenantColumn) =>
        tenantColumn is null
            ? new TenantColumnConstraint(IsRequired: false, IsSortOrderForced: false, ForcedSortOrder: null)
            : new TenantColumnConstraint(
                tenantColumn.IsRequired,
                tenantColumn.IsSortOrderForced,
                tenantColumn.IsSortOrderForced ? tenantColumn.SortOrder : null);

    public static IReadOnlyDictionary<string, TenantColumn> IndexByKey(
        IEnumerable<TenantColumn> tenantColumns) =>
        tenantColumns.ToDictionary(column => column.Key, StringComparer.OrdinalIgnoreCase);

    public static ErrorOr<Success> ValidateDeletion(ProjectColumn projectColumn, TenantColumn? tenantColumn)
    {
        if (tenantColumn?.IsRequired == true)
        {
            return Error.Validation(
                "ProjectColumn.Required",
                $"Column '{projectColumn.Title}' is required and cannot be removed from this project.");
        }

        return Result.Success;
    }

    public static Dictionary<Guid, int> ComputeSortOrders(
        IReadOnlyList<ProjectColumn> columnsInVisualOrder,
        IReadOnlyDictionary<string, TenantColumn> tenantColumnsByKey)
    {
        var sortOrders = new Dictionary<Guid, int>();
        var taken = new HashSet<int>();

        foreach (var column in columnsInVisualOrder)
        {
            if (!tenantColumnsByKey.TryGetValue(column.Key, out var tenantColumn) ||
                !tenantColumn.IsSortOrderForced)
            {
                continue;
            }

            sortOrders[column.Id] = tenantColumn.SortOrder;
            taken.Add(tenantColumn.SortOrder);
        }

        var availableSlots = new Queue<int>();
        var maxSortOrder = Math.Max(
            columnsInVisualOrder.Count,
            taken.Count > 0 ? taken.Max() + 1 : columnsInVisualOrder.Count);

        for (var slot = 0; slot <= maxSortOrder + columnsInVisualOrder.Count; slot++)
        {
            if (!taken.Contains(slot))
            {
                availableSlots.Enqueue(slot);
            }
        }

        foreach (var column in columnsInVisualOrder)
        {
            if (sortOrders.ContainsKey(column.Id))
            {
                continue;
            }

            sortOrders[column.Id] = availableSlots.Dequeue();
            taken.Add(sortOrders[column.Id]);
        }

        return sortOrders;
    }

    public static ErrorOr<Success> ValidateSortOrders(
        IReadOnlyDictionary<Guid, ProjectColumn> columnsById,
        IReadOnlyDictionary<Guid, int> sortOrders,
        IReadOnlyDictionary<string, TenantColumn> tenantColumnsByKey)
    {
        if (sortOrders.Values.Distinct().Count() != sortOrders.Count)
        {
            return Error.Validation("ProjectColumn.InvalidReorder", "Column sort orders must be unique.");
        }

        foreach (var (columnId, sortOrder) in sortOrders)
        {
            if (!columnsById.TryGetValue(columnId, out var column))
            {
                return Error.Validation("ProjectColumn.InvalidReorder", "One or more columns were not found.");
            }

            if (!tenantColumnsByKey.TryGetValue(column.Key, out var tenantColumn) ||
                !tenantColumn.IsSortOrderForced)
            {
                continue;
            }

            if (sortOrder != tenantColumn.SortOrder)
            {
                return Error.Validation(
                    "ProjectColumn.ForcedSortOrder",
                    $"Column '{column.Title}' must stay at position {tenantColumn.SortOrder + 1}.");
            }
        }

        return Result.Success;
    }
}
