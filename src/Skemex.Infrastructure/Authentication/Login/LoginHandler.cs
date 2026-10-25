using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginHandler(
    UserManager<User> userManager,
    AuthLoginSessionBuilder loginSessionBuilder)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<ErrorOr<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        if (!user.EmailConfirmed)
        {
            return Error.Unauthorized(UserErrors.EmailUnverified, UserErrors.EmailUnverifiedDescription);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Error.Unauthorized(UserErrors.InvalidPassword, UserErrors.InvalidPasswordDescription);
        }

        return await loginSessionBuilder.BuildAsync(user, cancellationToken).ConfigureAwait(false);
    }
}
