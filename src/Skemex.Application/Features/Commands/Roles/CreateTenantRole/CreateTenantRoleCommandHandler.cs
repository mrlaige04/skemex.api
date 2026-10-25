using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Roles.CreateTenantRole;

public sealed class CreateTenantRoleCommandHandler(
    ICurrentUser currentUser,
    RoleManager<Role> roleManager,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<Permission> permissionRepository,
    IBaseRepository<RolePermission> rolePermissionRepository)
    : ICommandHandler<CreateTenantRoleCommand, RoleDetailDto>
{
    public async Task<ErrorOr<RoleDetailDto>> Handle(
        CreateTenantRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing roles.");
        }

        var name = request.Name.Trim();
        if (string.Equals(name, RoleNames.SuperAdmin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("Role.ReservedName", $"Role name '{name}' is reserved.");
        }

        var nameTaken = await roleRepository.ExistsAsync(
            r => r.TenantId == tenantId && r.Name == name,
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

        var role = new Role
        {
            Name = name,
            TenantId = tenantId,
            IsSystem = false,
        };

        var create = await roleManager.CreateAsync(role);
        if (!create.Succeeded)
        {
            return Error.Validation(
                "Role.CreateFailed",
                string.Join(' ', create.Errors.Select(e => e.Description)));
        }

        if (permissions.Count > 0)
        {
            await rolePermissionRepository.AddRangeAsync(
                permissions.Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    RoleId = role.Id,
                    PermissionId = p.Id,
                }),
                cancellationToken);
        }

        return new RoleDetailDto
        {
            Id = role.Id,
            Name = role.Name ?? name,
            IsSystem = false,
            PermissionIds = permissions.Select(p => p.Id).ToList(),
            PermissionNames = permissions.Select(p => p.Name).OrderBy(n => n).ToList(),
        };
    }
}
