using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectColumns;

public sealed class GetProjectColumnsQuery : IQuery<IReadOnlyList<ProjectColumnDto>>
{
    public Guid ProjectId { get; init; }
}
