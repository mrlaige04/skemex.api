using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskByCode;

public sealed class GetProjectTaskByCodeQuery : IQuery<ProjectTaskDto>
{
    public Guid ProjectId { get; init; }
    public string Code { get; init; } = string.Empty;
}
