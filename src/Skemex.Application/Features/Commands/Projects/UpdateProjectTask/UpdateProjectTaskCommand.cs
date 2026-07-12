using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTask;

public sealed class UpdateProjectTaskCommand : ICommand<ProjectTaskDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Guid ColumnId { get; set; }
}
