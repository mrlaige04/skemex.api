using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Abstractions;

namespace Skemex.Application.SaFeatures.Queries.GetSaUsers;

public sealed class GetSaUsersQuery : IQuery<PaginatedList<SaUserDto>>
{
    public string? Search { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
