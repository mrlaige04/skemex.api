using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProject;

public sealed class DeleteProjectCommand : ICommand
{
    public Guid ProjectId { get; init; }
}
