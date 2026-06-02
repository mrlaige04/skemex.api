using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Web.Attributes;

public class PermissionFilter(
    ICurrentUser currentUser,
    IBaseRepository<User> userRepository,
    PermissionCombining combining,
    string[] permissions) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var rolesFromJwt = currentUser.GetRoles();
        var jwtIsSuper = rolesFromJwt?.Any(r => r == RoleNames.SuperAdmin) ?? false;

        var userEntity = await userRepository.GetAsync(
            u => u.Id == userId,
            q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
                .ThenInclude(rp => rp.Permission));

        var globalSuper = userEntity?.UserRoles.Any(ur =>
            ur.Role.Name == RoleNames.SuperAdmin && ur.TenantId == null) ?? false;

        if (globalSuper && jwtIsSuper)
        {
            return;
        }

        var tenantId = currentUser.GetTenantId();
        var userPermissions = userEntity?.UserRoles
            .Where(ur => ur.TenantId == null || ur.TenantId == tenantId)
            .SelectMany(ur => ur.Role.Permissions.Select(rp => rp.Permission))
            .ToList() ?? [];

        var hasPermission = combining == PermissionCombining.All
            ? permissions.All(p => userPermissions.Any(up => up.Name == p))
            : permissions.Any(p => userPermissions.Any(up => up.Name == p));

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}

public enum PermissionCombining
{
    AtLeastOne,
    All
}
