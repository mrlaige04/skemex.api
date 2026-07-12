using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Queries.Users.GetTenantUserById;

public sealed class GetTenantUserByIdQuery : IQuery<TenantUserDto>
{
    public Guid UserId { get; init; }
}
