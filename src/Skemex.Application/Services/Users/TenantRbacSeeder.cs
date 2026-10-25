using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Users;

/// <summary>
/// Seeds the tenant permission catalog and default roles.
/// On new tenant creation, Admin is always created with every permission in the catalog.
/// </summary>
public sealed class TenantRbacSeeder(
    RoleManager<Role> roleManager,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<PermissionGroup> permissionGroupRepository,
    IBaseRepository<Permission> permissionRepository,
    IBaseRepository<RolePermission> rolePermissionRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<Tenant> tenantRepository)
{
    public async Task EnsureForAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await tenantRepository.GetAllAsync(cancellationToken: cancellationToken);
        foreach (var tenant in tenants)
        {
            await EnsureDefaultsAsync(tenant.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Full seed for a brand-new tenant: permission catalog + Admin (all permissions)
    /// + Manager/Developer/QA starting grants. Safe to call only right after tenant insert.
    /// </summary>
    public async Task SeedForNewTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var permissionsByName = await EnsurePermissionCatalogAsync(tenantId, cancellationToken);
        var rolesByName = await EnsureDefaultRolesExistAsync(tenantId, cancellationToken);

        // Admin must have every permission in the tenant catalog.
        await ReplaceRolePermissionsAsync(
            rolesByName[RoleNames.Admin],
            permissionsByName.Values,
            cancellationToken);

        await EnsureRoleHasPermissionsAsync(
            rolesByName[RoleNames.Manager],
            PermissionNames.GetManagerGrants()
                .Where(permissionsByName.ContainsKey)
                .Select(n => permissionsByName[n]),
            cancellationToken);

        await EnsureRoleHasPermissionsAsync(
            rolesByName[RoleNames.Developer],
            PermissionNames.GetDeveloperGrants()
                .Where(permissionsByName.ContainsKey)
                .Select(n => permissionsByName[n]),
            cancellationToken);

        await EnsureRoleHasPermissionsAsync(
            rolesByName[RoleNames.QA],
            PermissionNames.GetDeveloperGrants()
                .Where(permissionsByName.ContainsKey)
                .Select(n => permissionsByName[n]),
            cancellationToken);
    }

    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await MigrateLegacyUserRoleAsync(tenantId, cancellationToken);
        var permissionsByName = await EnsurePermissionCatalogAsync(tenantId, cancellationToken);

        var hasAnyRole = await roleRepository.ExistsAsync(
            r => r.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (!hasAnyRole)
        {
            await SeedForNewTenantAsync(tenantId, cancellationToken);
            return;
        }

        var rolesByName = await EnsureDefaultRolesExistAsync(tenantId, cancellationToken);

        // Keep Admin in sync with the full catalog when new permissions are introduced.
        if (rolesByName.TryGetValue(RoleNames.Admin, out var admin))
        {
            await EnsureRoleHasPermissionsAsync(admin, permissionsByName.Values, cancellationToken);
        }

        // Ensure Manager exists on older tenants (additive grants).
        if (rolesByName.TryGetValue(RoleNames.Manager, out var manager))
        {
            await EnsureRoleHasPermissionsAsync(
                manager,
                PermissionNames.GetManagerGrants()
                    .Where(permissionsByName.ContainsKey)
                    .Select(n => permissionsByName[n]),
                cancellationToken);
        }
    }

    private async Task MigrateLegacyUserRoleAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var legacy = await roleRepository.GetAsync(
            r => r.TenantId == tenantId && r.Name == RoleNames.LegacyUser,
            cancellationToken: cancellationToken);
        if (legacy is null)
        {
            return;
        }

        var developer = await roleRepository.GetAsync(
            r => r.TenantId == tenantId && r.Name == RoleNames.Developer,
            cancellationToken: cancellationToken);

        if (developer is null)
        {
            legacy.Name = RoleNames.Developer;
            legacy.NormalizedName = RoleNames.Developer.ToUpperInvariant();
            legacy.IsSystem = false;
            await roleManager.UpdateAsync(legacy);
            return;
        }

        var assignments = await userRoleRepository.GetAllAsync(
            ur => ur.TenantId == tenantId && ur.RoleId == legacy.Id,
            cancellationToken: cancellationToken);

        foreach (var assignment in assignments)
        {
            var alreadyHasDeveloper = await userRoleRepository.ExistsAsync(
                ur => ur.TenantId == tenantId
                      && ur.UserId == assignment.UserId
                      && ur.RoleId == developer.Id,
                cancellationToken: cancellationToken);

            if (alreadyHasDeveloper)
            {
                await userRoleRepository.DeleteAsync(assignment, cancellationToken);
            }
            else
            {
                assignment.RoleId = developer.Id;
                await userRoleRepository.UpdateAsync(assignment, cancellationToken);
            }
        }

        var legacyPermissions = await rolePermissionRepository.GetAllAsync(
            rp => rp.RoleId == legacy.Id,
            cancellationToken: cancellationToken);
        if (legacyPermissions.Count > 0)
        {
            await rolePermissionRepository.DeleteRangeAsync(legacyPermissions, cancellationToken);
        }

        await roleManager.DeleteAsync(legacy);
    }

    private async Task<Dictionary<string, Permission>> EnsurePermissionCatalogAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var groups = await permissionGroupRepository.GetAllAsync(
            g => g.TenantId == tenantId,
            include: q => q.Include(g => g.Permissions),
            cancellationToken: cancellationToken);

        var groupsByName = groups.ToDictionary(g => g.Name, StringComparer.Ordinal);
        var permissionsByName = groups
            .SelectMany(g => g.Permissions)
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var (groupName, permissionNames) in PermissionGroupNames.GetCatalog())
        {
            if (!groupsByName.TryGetValue(groupName, out var group))
            {
                group = new PermissionGroup
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = groupName,
                    IsSystem = true,
                };
                await permissionGroupRepository.AddAsync(group, cancellationToken);
                groupsByName[groupName] = group;
            }

            foreach (var permissionName in permissionNames)
            {
                if (permissionsByName.ContainsKey(permissionName))
                {
                    continue;
                }

                var permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = permissionName,
                    GroupId = group.Id,
                    IsSystem = true,
                };
                await permissionRepository.AddAsync(permission, cancellationToken);
                permissionsByName[permissionName] = permission;
            }
        }

        return permissionsByName;
    }

    private async Task<Dictionary<string, Role>> EnsureDefaultRolesExistAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var rolesByName = new Dictionary<string, Role>(StringComparer.Ordinal);

        foreach (var roleName in RoleNames.GetTenantSystemRoles())
        {
            var role = await roleRepository.GetAsync(
                r => r.TenantId == tenantId && r.Name == roleName,
                cancellationToken: cancellationToken);

            var isSystemRole = string.Equals(roleName, RoleNames.Admin, StringComparison.Ordinal);

            if (role is null)
            {
                role = new Role
                {
                    Name = roleName,
                    TenantId = tenantId,
                    IsSystem = isSystemRole,
                };
                var create = await roleManager.CreateAsync(role);
                if (!create.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{roleName}' for tenant {tenantId}: "
                        + string.Join(' ', create.Errors.Select(e => e.Description)));
                }
            }
            else if (isSystemRole && !role.IsSystem)
            {
                role.IsSystem = true;
                var update = await roleManager.UpdateAsync(role);
                if (!update.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to mark role '{roleName}' as system for tenant {tenantId}: "
                        + string.Join(' ', update.Errors.Select(e => e.Description)));
                }
            }

            rolesByName[roleName] = role;
        }

        return rolesByName;
    }

    /// <summary>Replaces the role's permissions with exactly the given set (used for Admin = all).</summary>
    private async Task ReplaceRolePermissionsAsync(
        Role role,
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken)
    {
        var desired = permissions.DistinctBy(p => p.Id).ToList();
        var existing = await rolePermissionRepository.GetAllAsync(
            rp => rp.RoleId == role.Id,
            cancellationToken: cancellationToken);

        if (existing.Count > 0)
        {
            await rolePermissionRepository.DeleteRangeAsync(existing, cancellationToken);
        }

        if (desired.Count == 0)
        {
            return;
        }

        await rolePermissionRepository.AddRangeAsync(
            desired.Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = role.TenantId!.Value,
                RoleId = role.Id,
                PermissionId = p.Id,
            }),
            cancellationToken);
    }

    /// <summary>Adds missing grants without removing existing ones (Developer / QA).</summary>
    private async Task EnsureRoleHasPermissionsAsync(
        Role role,
        IEnumerable<Permission> permissions,
        CancellationToken cancellationToken)
    {
        var desired = permissions.DistinctBy(p => p.Id).ToList();
        if (desired.Count == 0)
        {
            return;
        }

        var existing = await rolePermissionRepository.GetAllAsync(
            rp => rp.RoleId == role.Id,
            cancellationToken: cancellationToken);
        var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();

        var toAdd = desired
            .Where(p => !existingIds.Contains(p.Id))
            .Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = role.TenantId!.Value,
                RoleId = role.Id,
                PermissionId = p.Id,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await rolePermissionRepository.AddRangeAsync(toAdd, cancellationToken);
        }
    }
}
