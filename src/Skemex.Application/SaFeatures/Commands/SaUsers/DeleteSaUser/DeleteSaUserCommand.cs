using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.DeleteSaUser;

public sealed class DeleteSaUserCommand : ICommand
{
    public Guid UserId { get; init; }
}
