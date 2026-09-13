using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTaskWorkLog;

public sealed class CreateProjectTaskWorkLogCommand : ICommand<ProjectTaskWorkLogDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public string? Comment { get; init; }
}
