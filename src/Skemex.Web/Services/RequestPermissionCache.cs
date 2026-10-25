using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Skemex.Application.Services.Users;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Services;

namespace Skemex.Web.Services;

/// <summary>
/// Per-request cache of tenant permissions loaded from the database.
/// </summary>
public sealed class RequestPermissionCache(
    IHttpContextAccessor httpContextAccessor,
    ITenantUserPermissionService permissionService)
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>(StringComparer.Ordinal);

    private IReadOnlySet<string>? _permissions;
    private bool _loaded;

    public IReadOnlySet<string> Permissions => _permissions ?? Empty;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        if (IsSuperAdmin())
        {
            _permissions = Empty;
            return;
        }

        var userId = GetUserId();
        var tenantId = GetTenantId();
        if (userId is null || tenantId is null)
        {
            _permissions = Empty;
            return;
        }

        _permissions = await permissionService.GetPermissionNamesAsync(
            userId.Value,
            tenantId.Value,
            cancellationToken);
    }

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    private Guid? GetUserId()
    {
        if (AmbientUserContext.UserId is { } ambientUserId)
        {
            return ambientUserId;
        }

        var value = User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private Guid? GetTenantId()
    {
        if (AmbientUserContext.TenantId is { } ambientTenantId)
        {
            return ambientTenantId;
        }

        var value = User?.FindFirstValue(CustomClaims.TenantId);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private bool IsSuperAdmin()
    {
        var value = User?.FindFirstValue(CustomClaims.IsSuperAdmin);
        return bool.TryParse(value, out var b) && b;
    }
}
