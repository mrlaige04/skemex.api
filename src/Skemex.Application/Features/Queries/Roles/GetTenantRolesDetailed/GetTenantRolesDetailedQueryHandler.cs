using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Roles.GetTenantRolesDetailed;

public sealed class GetTenantRolesDetailedQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Role> roleRepository,
    TenantRbacSeeder tenantRbacSeeder)
    : IQueryHandler<GetTenantRolesDetailedQuery, IReadOnlyList<RoleDetailDto>>
{
    public async Task<ErrorOr<IReadOnlyList<RoleDetailDto>>> Handle(
        GetTenantRolesDetailedQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing roles.");
        }

        await tenantRbacSeeder.EnsureDefaultsAsync(tenantId.Value, cancellationToken);

        var roles = await roleRepository.GetAllAsync(
            filter: r => r.TenantId == tenantId,
            include: q => q.Include(r => r.Permissions).ThenInclude(rp => rp.Permission),
            cancellationToken: cancellationToken);

        return roles
            .OrderBy(r => r.IsSystem ? 0 : 1)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDetailDto
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                IsSystem = r.IsSystem,
                PermissionIds = r.Permissions.Select(rp => rp.PermissionId).ToList(),
                PermissionNames = r.Permissions
                    .Select(rp => rp.Permission.Name)
                    .OrderBy(n => n)
                    .ToList(),
            })
            .ToList();
    }
}
