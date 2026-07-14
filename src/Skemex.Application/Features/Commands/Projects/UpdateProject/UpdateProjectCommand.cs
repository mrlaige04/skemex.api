using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProject;

public sealed class UpdateProjectCommand : ICommand<ProjectDto>
{
    public Guid ProjectId { get; init; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
