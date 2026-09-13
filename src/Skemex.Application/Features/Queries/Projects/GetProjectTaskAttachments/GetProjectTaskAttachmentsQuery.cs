using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskAttachments;

public sealed class GetProjectTaskAttachmentsQuery : IQuery<IReadOnlyList<ProjectTaskAttachmentDto>>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
}
