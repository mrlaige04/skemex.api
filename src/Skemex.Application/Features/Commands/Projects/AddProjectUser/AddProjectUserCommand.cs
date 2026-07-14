using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.AddProjectUser;

public sealed class AddProjectUserCommand : ICommand<ProjectUserDto>
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
}
