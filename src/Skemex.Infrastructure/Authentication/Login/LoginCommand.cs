using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginCommand : ICommand<LoginResponse>
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}
