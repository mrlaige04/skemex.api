using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Queries.GetTenantUserById;

public sealed class GetTenantUserByIdQuery : IQuery<TenantUserDto>
{
    public Guid UserId { get; init; }
}
