using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTask;

public sealed class CreateProjectTaskCommand : ICommand<ProjectTaskDto>
{
    public Guid ProjectId { get; init; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssigneeId { get; init; }
    public Guid? ParentId { get; init; }
}
