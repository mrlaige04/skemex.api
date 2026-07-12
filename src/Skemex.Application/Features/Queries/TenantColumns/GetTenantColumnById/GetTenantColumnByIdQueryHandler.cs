using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.TenantColumns.GetTenantColumnById;

public sealed class GetTenantColumnByIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : IQueryHandler<GetTenantColumnByIdQuery, TenantColumnDto>
{
    public async Task<ErrorOr<TenantColumnDto>> Handle(
        GetTenantColumnByIdQuery request,
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

        return GetTenantColumnsQueryHandler.MapToDto(column);
    }
}
