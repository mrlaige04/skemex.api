using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.App;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication.Models;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.RefreshToken;

public sealed class RefreshTokenHandler(
    UserManager<User> userManager,
    TokenService tokenService,
    AuthLoginSessionBuilder loginSessionBuilder,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<RefreshTokenCommand, AccessTokenResponse>
{
    public async Task<ErrorOr<AccessTokenResponse>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var context = await tokenService.TryReadRefreshContextAsync(request.AccessToken).ConfigureAwait(false);
        if (context is null)
        {
            return Error.Unauthorized(UserErrors.InvalidRefreshToken, UserErrors.InvalidRefreshTokenDescription);
        }

        var user = await userManager.FindByIdAsync(context.UserId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Error.Unauthorized(UserErrors.InvalidRefreshToken, UserErrors.InvalidRefreshTokenDescription);
        }

        if (string.IsNullOrEmpty(user.RefreshToken)
            || !string.Equals(user.RefreshToken, request.RefreshToken, StringComparison.Ordinal)
            || user.RefreshTokenExpiresAt is null
            || user.RefreshTokenExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Error.Unauthorized(UserErrors.InvalidRefreshToken, UserErrors.InvalidRefreshTokenDescription);
        }

        AccessTokenResponse token;
        if (context.TenantId is Guid tenantId)
        {
            token = await tokenService.GenerateTenantScopedToken(user, tenantId).ConfigureAwait(false);
        }
        else if (superAdminOptions.Value.MatchesEmail(user.Email))
        {
            token = await tokenService.GenerateGeneralLoginToken(user).ConfigureAwait(false);
        }
        else
        {
            // Upgrade legacy unscoped refresh sessions to a tenant-scoped token.
            var resolved = await loginSessionBuilder.ResolveTenantIdForUserAsync(user, cancellationToken)
                .ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            token = await tokenService.GenerateTenantScopedToken(user, resolved.Value).ConfigureAwait(false);
            user.LastUsedTenantId = resolved.Value;
        }

        tokenService.AssignRefreshToken(user, token);

        var update = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!update.Succeeded)
        {
            return Error.Failure(
                "Auth.RefreshFailed",
                string.Join(' ', update.Errors.Select(e => e.Description)));
        }

        return token;
    }
}
