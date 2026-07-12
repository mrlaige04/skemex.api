using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectById;

public sealed class GetProjectByIdQuery : IQuery<ProjectDto>
{
    public Guid ProjectId { get; init; }
}
