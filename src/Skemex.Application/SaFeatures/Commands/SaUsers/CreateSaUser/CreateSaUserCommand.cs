using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaUsers;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.CreateSaUser;

public sealed class CreateSaUserCommand : ICommand<SaUserDto>
{
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Password { get; set; }
}
