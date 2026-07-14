using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTask;

public sealed class UpdateProjectTaskCommand : ICommand<ProjectTaskDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Guid? ColumnId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public bool ClearDescription { get; init; }
    public Guid? AssigneeId { get; init; }
    public bool ClearAssignee { get; init; }
}
