using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantColumns.DeleteTenantColumn;

public sealed class DeleteTenantColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<DeleteTenantColumnCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteTenantColumnCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing columns.");
        }

        var column = await tenantColumnRepository.GetAsync(
            filter: entry => entry.Id == request.ColumnId,
            cancellationToken: cancellationToken);

        if (column is null)
        {
            return Error.NotFound("TenantColumn.NotFound", "Column was not found.");
        }

        await tenantColumnRepository.DeleteAsync(column, cancellationToken);

        var remainingColumns = await tenantColumnRepository.GetAllAsync(
            include: query => query.OrderBy(entry => entry.SortOrder).ThenBy(entry => entry.Title),
            cancellationToken: cancellationToken);

        for (var index = 0; index < remainingColumns.Count; index++)
        {
            remainingColumns[index].SortOrder = index;
        }

        if (remainingColumns.Count > 0)
        {
            await tenantColumnRepository.UpdateRangeAsync(remainingColumns, cancellationToken);
        }

        return Result.Success;
    }
}
