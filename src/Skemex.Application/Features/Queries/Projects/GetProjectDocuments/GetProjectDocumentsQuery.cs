using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Abstractions;

namespace Skemex.Application.Features.Queries.Projects.GetProjectDocuments;

public sealed class GetProjectDocumentsQuery : IQuery<PaginatedList<ProjectDocumentDto>>
{
    public Guid ProjectId { get; init; }
    public string? Search { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
