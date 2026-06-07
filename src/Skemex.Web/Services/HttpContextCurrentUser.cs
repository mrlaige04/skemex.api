using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication;

namespace Skemex.Web.Services;

public class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? GetTenantId()
    {
        var value = User?.FindFirstValue(CustomClaims.TenantId);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : null;
    }

    public Guid? GetUserId()
    {
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
    }

    public bool IsSuperAdmin()
    {
        var value = User?.FindFirstValue(CustomClaims.IsSuperAdmin);
        return bool.TryParse(value, out var b) && b;
    }
}
