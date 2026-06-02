using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication.Models;
using Skemex.Infrastructure.Authentication;

namespace Skemex.Infrastructure.Authentication.Services;

public class TokenService(
    IOptions<JwtOptions> jwtOptions,
    IBaseRepository<User> userRepository)
{
    public Task<AccessTokenResponse> GenerateGeneralLoginToken(User user) =>
        GenerateToken(user, tenantId: null);

    public Task<AccessTokenResponse> GenerateTenantScopedToken(User user, Guid tenantId) =>
        GenerateToken(user, tenantId);

    public async Task<AccessTokenResponse> GenerateToken(User user, Guid? tenantId)
    {
        var options = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var principal = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.Tenants));

        if (principal is null)
        {
            throw new InvalidOperationException("User not found for token issuance.");
        }

        var isSuperAdmin = principal.UserRoles.Any(ur =>
            ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin);

        var roleNames = CollectRoleNamesForToken(principal, tenantId);
        var roleClaims = roleNames
            .SelectMany(name => new Claim[]
            {
                new(ClaimTypes.Role, name),
                new("role", name),
            })
            .ToList();

        var tenantClaimValue = tenantId?.ToString() ?? string.Empty;

        var securityTokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(CustomClaims.TenantId, tenantClaimValue),
                ..roleClaims,
                new Claim(CustomClaims.IsSuperAdmin, isSuperAdmin ? "true" : "false"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(options.ExpiresInMinutes),
            SigningCredentials = credentials,
            Issuer = options.ValidIssuer,
            Audience = options.ValidAudience,
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(securityTokenDescriptor);
        return new AccessTokenResponse
        {
            AccessToken = token,
            TokenType = options.TokenType,
        };
    }

    private static HashSet<string> CollectRoleNamesForToken(User principal, Guid? effectiveTenantId)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ur in principal.UserRoles)
        {
            if (ur.TenantId is null)
            {
                if (ur.Role.Name == RoleNames.SuperAdmin)
                {
                    names.Add(ur.Role.Name!);
                }

                continue;
            }

            if (effectiveTenantId is not null && ur.TenantId == effectiveTenantId.Value)
            {
                names.Add(ur.Role.Name!);
            }
        }

        return names;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }

        return Convert.ToBase64String(randomNumber);
    }

    public string? GetEmailFromExpiredToken(string token)
    {
        var tokenHandler = new JsonWebTokenHandler();

        try
        {
            var readToken = tokenHandler.ReadJsonWebToken(token);
            return readToken.TryGetValue(JwtRegisteredClaimNames.Email, out string? email) ? email : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Reads user/tenant from access JWT; signature must be valid but expiry is ignored.</summary>
    public async Task<AccessTokenRefreshContext?> TryReadRefreshContextAsync(string accessToken)
    {
        var parameters = jwtOptions.Value.ToTokenValidationParameters();
        parameters.ValidateLifetime = false;

        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(accessToken, parameters).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return null;
        }

        var sub = result.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? result.ClaimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
        {
            return null;
        }

        Guid? tenantId = null;
        var tenantValue = result.ClaimsIdentity?.FindFirst(CustomClaims.TenantId)?.Value;
        if (!string.IsNullOrWhiteSpace(tenantValue) && Guid.TryParse(tenantValue, out var parsedTenant))
        {
            tenantId = parsedTenant;
        }

        return new AccessTokenRefreshContext(userId, tenantId);
    }
}

public sealed record AccessTokenRefreshContext(Guid UserId, Guid? TenantId);
