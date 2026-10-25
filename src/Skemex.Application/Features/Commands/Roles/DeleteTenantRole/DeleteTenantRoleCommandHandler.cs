using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Roles.DeleteTenantRole;

public sealed class DeleteTenantRoleCommandHandler(
    ICurrentUser currentUser,
    RoleManager<Role> roleManager,
    IBaseRepository<Role> roleRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IBaseRepository<RolePermission> rolePermissionRepository)
    : ICommandHandler<DeleteTenantRoleCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteTenantRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing roles.");
        }

        var role = await roleRepository.GetAsync(
            r => r.Id == request.RoleId && r.TenantId == tenantId,
            include: q => q.Include(r => r.Permissions),
            cancellationToken: cancellationToken);

        if (role is null)
        {
            return Error.NotFound("Role.NotFound", "Role was not found.");
        }

        if (role.IsSystem)
        {
            return Error.Forbidden(
                "Role.SystemProtected",
                "System roles cannot be deleted.");
        }

        var assigned = await userRoleRepository.ExistsAsync(
            ur => ur.RoleId == role.Id && ur.TenantId == tenantId,
            cancellationToken: cancellationToken);
        if (assigned)
        {
            return Error.Conflict(
                "Role.InUse",
                "This role is assigned to one or more users. Reassign them before deleting.");
        }

        if (role.Permissions.Count > 0)
        {
            await rolePermissionRepository.DeleteRangeAsync(role.Permissions, cancellationToken);
        }

        var delete = await roleManager.DeleteAsync(role);
        if (!delete.Succeeded)
        {
            return Error.Validation(
                "Role.DeleteFailed",
                string.Join(' ', delete.Errors.Select(e => e.Description)));
        }

        return Result.Success;
    }
}
