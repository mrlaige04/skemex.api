using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class RequestPasswordResetHandler(
    UserManager<User> userManager,
    IAuthEmailService authEmailService,
    ILogger<RequestPasswordResetHandler> logger)
    : ICommandHandler<RequestPasswordResetCommand, RequestPasswordResetResponse>
{
    public async Task<ErrorOr<RequestPasswordResetResponse>> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user is not null)
        {
            try
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
                await authEmailService
                    .SendPasswordResetCodeAsync(user, code, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send password reset code to {Email}.", email);
            }
        }

        return new RequestPasswordResetResponse();
    }
}
