using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTaskComment;

public sealed class CreateProjectTaskCommentCommand : ICommand<ProjectTaskCommentDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public string Body { get; init; } = string.Empty;
}
