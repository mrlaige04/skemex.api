using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTaskWorkLog;

public sealed class UpdateProjectTaskWorkLogCommand : ICommand<ProjectTaskWorkLogDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Guid WorkLogId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public string? Comment { get; init; }
    public bool ClearComment { get; init; }
}
