using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.App;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterHandler(
    UserManager<User> userManager,
    IOptions<SuperAdminOptions> superAdminOptions,
    IAuthEmailService authEmailService,
    TenantProvisioningService tenantProvisioning,
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
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Error.Conflict(UserErrors.EmailAlreadyExists, UserErrors.EmailAlreadyExistsDescription);
            }

            var description = string.Join(" ", result.Errors.Select(e => e.Description));
            return Error.Validation(UserErrors.RegistrationFailed, description);
        }

        var workspace = await tenantProvisioning.CreatePersonalWorkspaceAsync(user, cancellationToken)
            .ConfigureAwait(false);
        if (workspace.IsError)
        {
            logger.LogError(
                "User {Email} was created but personal workspace provisioning failed: {Errors}",
                user.Email,
                string.Join("; ", workspace.Errors.Select(e => e.Description)));
            return workspace.Errors;
        }

        user.LastUsedTenantId = workspace.Value.Id;
        await userManager.UpdateAsync(user).ConfigureAwait(false);

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
}
