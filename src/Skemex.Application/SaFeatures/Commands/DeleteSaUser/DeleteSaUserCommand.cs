using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaUser;

public sealed class DeleteSaUserCommand : ICommand
{
    public Guid UserId { get; init; }
}
