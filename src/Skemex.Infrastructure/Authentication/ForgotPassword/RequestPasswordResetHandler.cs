using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
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
