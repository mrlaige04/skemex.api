using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaUsers;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.UpdateSaUser;

public sealed class UpdateSaUserCommand : ICommand<SaUserDto>
{
    public Guid UserId { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
