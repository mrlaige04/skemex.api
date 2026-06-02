using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterCommand : ICommand<RegisterResponse>
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
}
