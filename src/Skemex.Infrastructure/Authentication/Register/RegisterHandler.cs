using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterHandler(
    UserManager<User> userManager,
    IAuthEmailService authEmailService,
    ILogger<RegisterHandler> logger) : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<ErrorOr<RegisterResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
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
