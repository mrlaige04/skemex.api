using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
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
using Skemex.Infrastructure.Authentication;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterHandler(
    UserManager<User> userManager,
    IOptions<SuperAdminOptions> superAdminOptions,
    IAuthEmailService authEmailService,
    ILogger<RegisterHandler> logger) : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<ErrorOr<RegisterResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (superAdminOptions.Value.MatchesEmail(request.Email))
        {
            return Error.Validation(
                UserErrors.SuperAdminEmailReserved,
                UserErrors.SuperAdminEmailReservedDescription);
        }

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            try
            {
                await authEmailService.SendRegistrationGreetingAsync(user, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Registration succeeded but greeting email was not sent for {Email}.", user.Email);
            }

            return new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
            };
        }

        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return Error.Conflict(UserErrors.EmailAlreadyExists, UserErrors.EmailAlreadyExistsDescription);
        }

        var description = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation(UserErrors.RegistrationFailed, description);
    }
}
