using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskWorkLogs;

public sealed class GetProjectTaskWorkLogsQuery : IQuery<IReadOnlyList<ProjectTaskWorkLogDto>>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
}
