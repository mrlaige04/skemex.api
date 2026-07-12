using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTasksByColumnId;

public sealed class GetProjectTasksByColumnIdQuery : IQuery<IReadOnlyList<ProjectTaskDto>>
{
    public Guid ProjectId { get; init; }
    public Guid ColumnId { get; init; }
}
