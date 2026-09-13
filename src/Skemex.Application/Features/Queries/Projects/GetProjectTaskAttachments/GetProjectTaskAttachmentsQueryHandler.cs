using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskAttachments;

public sealed class GetProjectTaskAttachmentsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTaskAttachmentsQuery, IReadOnlyList<ProjectTaskAttachmentDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectTaskAttachmentDto>>> Handle(
        GetProjectTaskAttachmentsQuery request,
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

        var taskExists = await taskRepository.ExistsAsync(
            filter: task => task.Id == request.TaskId && task.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!taskExists)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var attachments = await attachmentRepository.GetAllAsync(
            filter: entry => entry.ProjectId == request.ProjectId && entry.TaskId == request.TaskId,
            include: query => query
                .Include(entry => entry.UploadedBy)
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenBy(entry => entry.FileName),
            cancellationToken: cancellationToken);

        var mapped = new List<ProjectTaskAttachmentDto>(attachments.Count);
        foreach (var attachment in attachments)
        {
            mapped.Add(
                new ProjectTaskAttachmentDto
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
                });
        }

        return mapped;
    }
}
