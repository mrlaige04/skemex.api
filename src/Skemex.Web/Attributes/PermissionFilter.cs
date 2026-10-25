using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Skemex.Domain.Services;
using Skemex.Web.Services;

namespace Skemex.Web.Attributes;

public class PermissionFilter(
    ICurrentUser currentUser,
    RequestPermissionCache permissionCache,
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

        if (currentUser.IsSuperAdmin())
        {
            return;
        }

        if (permissions.Length == 0)
        {
            return;
        }

        await permissionCache.EnsureLoadedAsync(context.HttpContext.RequestAborted);

        var userPermissions = permissionCache.Permissions;
        var hasPermission = combining == PermissionCombining.All
            ? permissions.All(p => userPermissions.Contains(p))
            : permissions.Any(p => userPermissions.Contains(p));

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
