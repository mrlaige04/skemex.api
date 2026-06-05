using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication.Tokens;

namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class ResetPasswordWithCodeHandler(UserManager<User> userManager)
    : ICommandHandler<ResetPasswordWithCodeCommand, ResetPasswordWithCodeResponse>
{
    public async Task<ErrorOr<ResetPasswordWithCodeResponse>> Handle(
        ResetPasswordWithCodeCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            return Error.Validation(
                "PasswordReset.InvalidCode",
                "The reset code is invalid or has expired.");
        }

        var result = await userManager
            .ResetPasswordAsync(user, request.Code.Trim(), request.NewPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            var invalidToken = result.Errors.Any(e =>
                e.Code is "InvalidToken" or "InvalidUserToken" or "TokenExpired");

            if (invalidToken)
            {
                return Error.Validation(
                    "PasswordReset.InvalidCode",
                    "The reset code is invalid or has expired.");
            }

            return Error.Validation(
                "PasswordReset.Failed",
                string.Join(' ', result.Errors.Select(e => e.Description)));
        }

        await userManager
            .RemoveAuthenticationTokenAsync(
                user,
                NumericEmailTokenProvider<User>.ProviderName,
                UserManager<User>.ResetPasswordTokenPurpose)
            .ConfigureAwait(false);

        return new ResetPasswordWithCodeResponse();
    }
}
