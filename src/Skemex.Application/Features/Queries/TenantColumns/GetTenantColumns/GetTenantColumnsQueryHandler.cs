using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;

public sealed class GetTenantColumnsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : IQueryHandler<GetTenantColumnsQuery, IReadOnlyList<TenantColumnDto>>
{
    public async Task<ErrorOr<IReadOnlyList<TenantColumnDto>>> Handle(
        GetTenantColumnsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing columns.");
        }

        var columns = await tenantColumnRepository.GetAllAsync(
            include: query => query.OrderBy(column => column.SortOrder).ThenBy(column => column.Title),
            cancellationToken: cancellationToken);

        return columns.Select(MapToDto).ToList();
    }

    internal static TenantColumnDto MapToDto(TenantColumn column) => new()
    {
        Id = column.Id,
        Key = column.Key,
        Title = column.Title,
        Description = column.Description,
        SortOrder = column.SortOrder,
        IsRequired = column.IsRequired,
        IsSortOrderForced = column.IsSortOrderForced,
    };
}
