using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Queries.GetTenantRoles;

public sealed class GetTenantRolesQuery : IQuery<IReadOnlyList<TenantRoleDto>>;
