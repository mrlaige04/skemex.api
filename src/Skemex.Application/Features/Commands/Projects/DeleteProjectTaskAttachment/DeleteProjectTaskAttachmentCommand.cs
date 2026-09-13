using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTaskAttachment;

public sealed class DeleteProjectTaskAttachmentCommand : ICommand
{
    public Guid ProjectId { get; init; }
    public Guid TaskId { get; init; }
    public Guid AttachmentId { get; init; }
}
