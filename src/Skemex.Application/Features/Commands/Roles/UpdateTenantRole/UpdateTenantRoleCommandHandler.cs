using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Roles.UpdateTenantRole;

public sealed class UpdateTenantRoleCommandHandler(
    ICurrentUser currentUser,
    RoleManager<Role> roleManager,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<Permission> permissionRepository,
    IBaseRepository<RolePermission> rolePermissionRepository)
    : ICommandHandler<UpdateTenantRoleCommand, RoleDetailDto>
{
    public async Task<ErrorOr<RoleDetailDto>> Handle(
        UpdateTenantRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing roles.");
        }

        var role = await roleRepository.GetAsync(
            r => r.Id == request.RoleId && r.TenantId == tenantId,
            include: q => q.Include(r => r.Permissions).ThenInclude(rp => rp.Permission),
            cancellationToken: cancellationToken);

        if (role is null)
        {
            return Error.NotFound("Role.NotFound", "Role was not found.");
        }

        if (role.IsSystem)
        {
            return Error.Forbidden(
                "Role.SystemProtected",
                "System roles cannot be edited.");
        }

        var name = request.Name.Trim();
        if (string.Equals(name, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("Role.ReservedName", $"Role name '{name}' is reserved.");
        }

        var nameTaken = await roleRepository.ExistsAsync(
            r => r.TenantId == tenantId && r.Name == name && r.Id != role.Id,
            cancellationToken: cancellationToken);
        if (nameTaken)
        {
            return Error.Conflict("Role.NameTaken", "A role with this name already exists.");
        }

        var permissionIds = request.PermissionIds.Distinct().ToList();
        var permissions = await permissionRepository.GetAllAsync(
            p => p.TenantId == tenantId && permissionIds.Contains(p.Id),
            cancellationToken: cancellationToken);

        if (permissions.Count != permissionIds.Count)
        {
            return Error.Validation("Role.PermissionsInvalid", "One or more permissions are invalid.");
        }

        if (!string.Equals(role.Name, name, StringComparison.Ordinal))
        {
            role.Name = name;
            var update = await roleManager.UpdateAsync(role);
            if (!update.Succeeded)
            {
                return Error.Validation(
                    "Role.UpdateFailed",
                    string.Join(' ', update.Errors.Select(e => e.Description)));
            }
        }

        var existing = role.Permissions.ToList();
        var desired = permissions.Select(p => p.Id).ToHashSet();
        var toRemove = existing.Where(rp => !desired.Contains(rp.PermissionId)).ToList();
        if (toRemove.Count > 0)
        {
            await rolePermissionRepository.DeleteRangeAsync(toRemove, cancellationToken);
        }

        var existingIds = existing.Select(rp => rp.PermissionId).ToHashSet();
        var toAdd = permissions
            .Where(p => !existingIds.Contains(p.Id))
            .Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                RoleId = role.Id,
                PermissionId = p.Id,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await rolePermissionRepository.AddRangeAsync(toAdd, cancellationToken);
        }

        return new RoleDetailDto
        {
            Id = role.Id,
            Name = name,
            IsSystem = role.IsSystem,
            PermissionIds = permissions.Select(p => p.Id).ToList(),
            PermissionNames = permissions.Select(p => p.Name).OrderBy(n => n).ToList(),
        };
    }
}
