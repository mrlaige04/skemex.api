using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantColumns.ReorderTenantColumns;

public sealed class ReorderTenantColumnsCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<ReorderTenantColumnsCommand, IReadOnlyList<TenantColumnDto>>
{
    public async Task<ErrorOr<IReadOnlyList<TenantColumnDto>>> Handle(
        ReorderTenantColumnsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing columns.");
        }

        var existingColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        if (existingColumns.Count == 0)
        {
            return [];
        }

        if (request.ColumnIds.Count != existingColumns.Count)
        {
            return Error.Validation(
                "TenantColumn.InvalidReorder",
                "Provide every column id in the new order.");
        }

        var columnsById = existingColumns.ToDictionary(column => column.Id);
        if (request.ColumnIds.Any(id => !columnsById.ContainsKey(id)))
        {
            return Error.Validation(
                "TenantColumn.InvalidReorder",
                "One or more columns were not found in this workspace.");
        }

        await using var transaction = await tenantColumnRepository.BeginTransactionAsync(cancellationToken);

        for (var index = 0; index < existingColumns.Count; index++)
        {
            existingColumns[index].SortOrder = -(index + 1);
        }

        await tenantColumnRepository.UpdateRangeAsync(existingColumns, cancellationToken);

        var reorderedColumns = new List<TenantColumn>(request.ColumnIds.Count);
        for (var index = 0; index < request.ColumnIds.Count; index++)
        {
            var column = columnsById[request.ColumnIds[index]];
            column.SortOrder = index;
            reorderedColumns.Add(column);
        }

        await tenantColumnRepository.UpdateRangeAsync(reorderedColumns, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return reorderedColumns
            .OrderBy(column => column.SortOrder)
            .Select(GetTenantColumnsQueryHandler.MapToDto)
            .ToList();
    }
}
