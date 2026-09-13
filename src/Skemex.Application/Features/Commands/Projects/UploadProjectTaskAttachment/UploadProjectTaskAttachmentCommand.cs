using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.Projects.UploadProjectTaskAttachment;

public sealed class UploadProjectTaskAttachmentCommand : ICommand<ProjectTaskAttachmentDto>
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Stream? FileContent { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
}
