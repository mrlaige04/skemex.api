using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.UpdateSaUser;

public sealed class UpdateSaUserCommand : ICommand<SaUserDto>
{
    public Guid UserId { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
