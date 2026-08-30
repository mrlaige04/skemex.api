using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetProjectDocumentById;

public sealed class GetProjectDocumentByIdQuery : IQuery<ProjectDocumentDto>
{
    public Guid ProjectId { get; set; }

    public Guid DocumentId { get; set; }
}
