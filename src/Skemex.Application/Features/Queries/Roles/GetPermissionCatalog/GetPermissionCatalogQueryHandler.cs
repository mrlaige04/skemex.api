using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Roles.GetPermissionCatalog;

public sealed class GetPermissionCatalogQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<PermissionGroup> permissionGroupRepository,
    TenantRbacSeeder tenantRbacSeeder)
    : IQueryHandler<GetPermissionCatalogQuery, IReadOnlyList<PermissionGroupDto>>
{
    public async Task<ErrorOr<IReadOnlyList<PermissionGroupDto>>> Handle(
        GetPermissionCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing roles.");
        }

        await tenantRbacSeeder.EnsureDefaultsAsync(tenantId.Value, cancellationToken);

        var groups = await permissionGroupRepository.GetAllAsync(
            filter: g => g.TenantId == tenantId,
            include: q => q.Include(g => g.Permissions),
            cancellationToken: cancellationToken);

        return groups
            .OrderBy(g => g.Name)
            .Select(g => new PermissionGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Permissions = g.Permissions
                    .OrderBy(p => p.Name)
                    .Select(p => new PermissionItemDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsSystem = p.IsSystem,
                    })
                    .ToList(),
            })
            .ToList();
    }
}
