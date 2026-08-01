using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Services;

namespace Skemex.Web.Services;

/// <summary>
/// Resolves current user from HTTP claims, with AmbientUserContext override for Hangfire jobs.
/// </summary>
public class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? GetTenantId()
    {
        if (AmbientUserContext.TenantId is { } ambientTenantId)
        {
            return ambientTenantId;
        }

        var value = User?.FindFirstValue(CustomClaims.TenantId);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public Guid? GetUserId()
    {
        if (AmbientUserContext.UserId is { } ambientUserId)
        {
            return ambientUserId;
        }

        var value = User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public string[]? GetRoles()
    {
        if (User?.Identity is not ClaimsIdentity identity)
        {
            return null;
        }

        var roles = identity.Claims
            .Where(c => c.Type is ClaimTypes.Role or "role" or CustomClaims.Roles)
            .Select(c => c.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return roles.Length == 0 ? null : roles;
    }

    public void SetTenantId(Guid? tenantId)
    {
        // HTTP path is claim-based; ambient override is set via AmbientUserContext.Use in jobs.
    }

    public bool IsSuperAdmin()
    {
        var value = User?.FindFirstValue(CustomClaims.IsSuperAdmin);
        return bool.TryParse(value, out var b) && b;
    }
}
