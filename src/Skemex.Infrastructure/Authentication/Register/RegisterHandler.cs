using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterHandler(UserManager<User> userManager) : ICommandHandler<RegisterCommand, RegisterResponse>
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
