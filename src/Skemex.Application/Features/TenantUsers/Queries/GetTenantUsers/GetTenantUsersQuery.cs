using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Queries.GetTenantUsers;

public sealed class GetTenantUsersQuery : IQuery<PaginatedList<TenantUserDto>>
{
    public string? Search { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
