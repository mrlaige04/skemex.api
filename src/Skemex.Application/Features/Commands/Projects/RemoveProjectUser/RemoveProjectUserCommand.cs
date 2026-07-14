using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.RemoveProjectUser;

public sealed class RemoveProjectUserCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
}
