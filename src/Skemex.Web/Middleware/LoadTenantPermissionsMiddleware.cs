using Skemex.Web.Services;

namespace Skemex.Web.Middleware;

/// <summary>
/// Loads the caller's tenant permissions from the database once per request
/// so authorization reflects live role grants (not stale JWT claims).
/// </summary>
public sealed class LoadTenantPermissionsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestPermissionCache permissionCache)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await permissionCache.EnsureLoadedAsync(context.RequestAborted);
        }

        await next(context);
    }
}
