using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication.Models;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.RefreshToken;

public sealed class RefreshTokenHandler(
    UserManager<User> userManager,
    TokenService tokenService)
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

        var token = context.TenantId is Guid tenantId
            ? await tokenService.GenerateTenantScopedToken(user, tenantId).ConfigureAwait(false)
            : await tokenService.GenerateGeneralLoginToken(user).ConfigureAwait(false);

        token.RefreshToken = tokenService.GenerateRefreshToken();
        user.RefreshToken = token.RefreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(2);

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
