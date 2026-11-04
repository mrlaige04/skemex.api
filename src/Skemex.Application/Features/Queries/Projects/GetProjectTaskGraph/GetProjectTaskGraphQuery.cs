using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskGraph;

public sealed class GetProjectTaskGraphQuery : IQuery<ProjectTaskGraphDto>
{
    public Guid ProjectId { get; init; }
}
