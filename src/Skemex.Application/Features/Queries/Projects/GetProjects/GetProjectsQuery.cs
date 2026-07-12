using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Abstractions;

namespace Skemex.Application.Features.Queries.Projects.GetProjects;

public sealed class GetProjectsQuery : IQuery<PaginatedList<ProjectDto>>
{
    public string? Search { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
