using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;

namespace Skemex.Application.Features.Queries.Roles.GetTenantRolesDetailed;

public sealed class GetTenantRolesDetailedQuery : IQuery<IReadOnlyList<RoleDetailDto>>;
