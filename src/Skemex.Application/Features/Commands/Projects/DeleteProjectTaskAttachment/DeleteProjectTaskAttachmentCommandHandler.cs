using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTaskAttachment;

public sealed class DeleteProjectTaskAttachmentCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    IIssueAttachmentStorageService attachmentStorage)
    : ICommandHandler<DeleteProjectTaskAttachmentCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectTaskAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var attachment = await attachmentRepository.GetAsync(
            filter: entry =>
                entry.Id == request.AttachmentId
                && entry.ProjectId == request.ProjectId
                && entry.TaskId == request.TaskId,
            cancellationToken: cancellationToken);
        if (attachment is null)
        {
            return Error.NotFound("ProjectTaskAttachment.NotFound", "Attachment was not found.");
        }

        var blobId = attachment.BlobId;
        await using var transaction = await attachmentRepository
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await attachmentRepository.DeleteAsync(attachment, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            await attachmentStorage.DeleteAsync(blobId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            /* best-effort blob cleanup */
        }

        return Result.Success;
    }
}
