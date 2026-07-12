using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.ReorderProjectColumns;

public sealed class ReorderProjectColumnsCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<ReorderProjectColumnsCommand, IReadOnlyList<ProjectColumnDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectColumnDto>>> Handle(
        ReorderProjectColumnsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var existingColumns = await projectColumnRepository.GetAllAsync(
            filter: column => column.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);

        if (existingColumns.Count == 0)
        {
            return [];
        }

        if (request.ColumnIds.Count != existingColumns.Count)
        {
            return Error.Validation(
                "ProjectColumn.InvalidReorder",
                "Provide every column id in the new order.");
        }

        var columnsById = existingColumns.ToDictionary(column => column.Id);
        if (request.ColumnIds.Any(id => !columnsById.ContainsKey(id)))
        {
            return Error.Validation(
                "ProjectColumn.InvalidReorder",
                "One or more columns were not found in this project.");
        }

        var tenantColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        var tenantColumnsByKey = TenantColumnConstraints.IndexByKey(tenantColumns);

        var columnsInVisualOrder = request.ColumnIds
            .Select(id => columnsById[id])
            .ToList();

        var sortOrders = TenantColumnConstraints.ComputeSortOrders(columnsInVisualOrder, tenantColumnsByKey);
        var validation = TenantColumnConstraints.ValidateSortOrders(columnsById, sortOrders, tenantColumnsByKey);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        await using var transaction = await projectColumnRepository.BeginTransactionAsync(cancellationToken);

        for (var index = 0; index < existingColumns.Count; index++)
        {
            existingColumns[index].SortOrder = -(index + 1);
        }

        await projectColumnRepository.UpdateRangeAsync(existingColumns, cancellationToken);

        var updatedColumns = new List<ProjectColumn>(columnsInVisualOrder.Count);
        foreach (var column in columnsInVisualOrder)
        {
            column.SortOrder = sortOrders[column.Id];
            updatedColumns.Add(column);
        }

        await projectColumnRepository.UpdateRangeAsync(updatedColumns, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return updatedColumns
            .OrderBy(column => column.SortOrder)
            .ThenBy(column => column.Title)
            .Select(column => ProjectColumnDtoMapper.Map(column, tenantColumnsByKey))
            .ToList();
    }
}
