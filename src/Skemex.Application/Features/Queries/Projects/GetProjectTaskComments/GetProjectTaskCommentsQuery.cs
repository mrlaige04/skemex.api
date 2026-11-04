using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskComments;

public sealed class GetProjectTaskCommentsQuery : IQuery<IReadOnlyList<ProjectTaskCommentDto>>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
}
