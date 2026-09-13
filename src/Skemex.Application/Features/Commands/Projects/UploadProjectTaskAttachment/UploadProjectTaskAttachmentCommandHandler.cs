using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UploadProjectTaskAttachment;

public sealed class UploadProjectTaskAttachmentCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    IIssueAttachmentStorageService attachmentStorage,
    IUrlService urlService)
    : ICommandHandler<UploadProjectTaskAttachmentCommand, ProjectTaskAttachmentDto>
{
    private const long MaxFileBytes = 25 * 1024 * 1024;

    public async Task<ErrorOr<ProjectTaskAttachmentDto>> Handle(
        UploadProjectTaskAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCore(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.FileContent?.Dispose();
        }
    }

    private async Task<ErrorOr<ProjectTaskAttachmentDto>> HandleCore(
        UploadProjectTaskAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before uploading attachments.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: entry => entry.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var taskExists = await taskRepository.ExistsAsync(
            filter: entry => entry.Id == request.TaskId && entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!taskExists)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var validation = ValidateFile(request);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? "application/octet-stream"
            : request.ContentType.Trim();
        if (contentType.Length > 128)
        {
            contentType = contentType[..128];
        }

        var fileName = Path.GetFileName(request.FileName?.Trim() ?? "attachment");
        if (fileName.Length == 0)
        {
            fileName = "attachment";
        }

        if (fileName.Length > 256)
        {
            fileName = fileName[..256];
        }

        var fileSize = request.FileContent!.Length;

        string blobId;
        try
        {
            blobId = await attachmentStorage
                .CreateAsync(
                    tenantId.Value,
                    request.ProjectId,
                    request.TaskId,
                    request.FileContent,
                    contentType,
                    fileName,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return Error.Unexpected(
                "ProjectTaskAttachment.UploadFailed",
                "Could not upload the attachment to storage.");
        }

        var attachment = new ProjectTaskAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            BlobId = blobId,
            UploadedById = userId.Value,
        };

        await attachmentRepository.AddAsync(attachment, cancellationToken);

        var created = await attachmentRepository.GetAsync(
            filter: entry => entry.Id == attachment.Id,
            include: query => query.Include(entry => entry.UploadedBy),
            cancellationToken: cancellationToken);

        if (created is null)
        {
            return Error.Unexpected(
                "ProjectTaskAttachment.CreateFailed",
                "Attachment was uploaded but could not be loaded.");
        }

        return await MapDtoAsync(created, cancellationToken).ConfigureAwait(false);
    }

    private static ErrorOr<Success> ValidateFile(UploadProjectTaskAttachmentCommand request)
    {
        if (request.FileContent is null || !request.FileContent.CanRead)
        {
            return Error.Validation("ProjectTaskAttachment.FileRequired", "A file is required.");
        }

        if (!request.FileContent.CanSeek)
        {
            return Error.Validation(
                "ProjectTaskAttachment.InvalidFile",
                "Uploaded file could not be read.");
        }

        if (request.FileContent.Length == 0)
        {
            return Error.Validation("ProjectTaskAttachment.EmptyFile", "Uploaded file is empty.");
        }

        if (request.FileContent.Length > MaxFileBytes)
        {
            return Error.Validation(
                "ProjectTaskAttachment.FileTooLarge",
                "File size cannot exceed 25 MB.");
        }

        return Result.Success;
    }

    private async Task<ProjectTaskAttachmentDto> MapDtoAsync(
        ProjectTaskAttachment attachment,
        CancellationToken cancellationToken) =>
        new()
        {
            Id = attachment.Id,
            ProjectId = attachment.ProjectId,
            TaskId = attachment.TaskId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            CreatedAt = attachment.CreatedAt,
            DownloadUrl = await urlService
                .GetIssueAttachmentUrlAsync(attachment.BlobId, cancellationToken)
                .ConfigureAwait(false),
            UploadedBy = new ProjectTaskAttachmentUserDto
            {
                Id = attachment.UploadedBy.Id,
                FirstName = attachment.UploadedBy.FirstName,
                LastName = attachment.UploadedBy.LastName,
                Email = attachment.UploadedBy.Email ?? string.Empty,
            },
        };
}
