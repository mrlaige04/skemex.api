using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Queries.Users.GetTenantRoles;

public sealed class GetTenantRolesQuery : IQuery<IReadOnlyList<TenantRoleDto>>;
