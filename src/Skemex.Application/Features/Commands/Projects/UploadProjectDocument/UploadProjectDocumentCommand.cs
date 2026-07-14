using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UploadProjectDocument;

public sealed class UploadProjectDocumentCommand : ICommand<ProjectDocumentDto>
{
    public Guid ProjectId { get; init; }
    public Stream? FileContent { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
}
