using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.CreateSaUser;

public sealed class CreateSaUserCommand : ICommand<SaUserDto>
{
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Password { get; set; }
}
