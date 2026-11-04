using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Application.Services.Storage;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

public sealed class JiraAttachmentCreatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IJiraApiClient jiraApiClient,
    IIssueAttachmentStorageService attachmentStorage,
    ILogger<JiraAttachmentCreatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    private const long MaxBytes = 25 * 1024 * 1024;

    public string EventKey => JiraWebhookEventCatalog.AttachmentCreated;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!TryReadAttachment(context.Payload, out var attachmentId, out var attachmentEl)
            || string.IsNullOrWhiteSpace(attachmentId))
        {
            return Result.Success;
        }

        var (issueKey, issueId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var taskMapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            issueKey,
            issueId,
            cancellationToken);
        if (taskMapping is null)
        {
            return Result.Success;
        }

        var existing = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == context.Connection.Id
                && m.LocalEntityType == IntegrationLocalEntityTypes.TaskAttachment
                && m.ExternalEntityId == attachmentId,
            cancellationToken: cancellationToken);
        if (existing is not null)
        {
            return Result.Success;
        }

        if (string.IsNullOrWhiteSpace(context.Connection.CloudId))
        {
            return Result.Success;
        }

        var downloaded = await jiraApiClient.DownloadAttachmentAsync(
            context.AccessToken,
            context.Connection.CloudId!,
            attachmentId,
            cancellationToken);
        if (downloaded is null)
        {
            logger.LogWarning("Could not download Jira attachment {AttachmentId}", attachmentId);
            return Result.Success;
        }

        await using var content = downloaded.Value.Content;
        if (content.CanSeek && content.Length > MaxBytes)
        {
            logger.LogWarning(
                "Skipping Jira attachment {AttachmentId}: size {Size} exceeds limit",
                attachmentId,
                content.Length);
            return Result.Success;
        }

        var fileName = downloaded.Value.FileName
            ?? (attachmentEl.TryGetProperty("filename", out var fnEl) ? fnEl.GetString() : null)
            ?? $"jira-attachment-{attachmentId}";
        var contentType = downloaded.Value.ContentType;
        if (attachmentEl.TryGetProperty("mimeType", out var mimeEl)
            && mimeEl.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(mimeEl.GetString()))
        {
            contentType = mimeEl.GetString()!;
        }

        var blobId = await attachmentStorage.CreateAsync(
            context.Connection.TenantId,
            context.Connection.ProjectId,
            taskMapping.LocalEntityId,
            content,
            contentType,
            fileName,
            cancellationToken);

        var size = content.CanSeek ? content.Length : 0L;
        if (attachmentEl.TryGetProperty("size", out var sizeEl)
            && sizeEl.TryGetInt64(out var declaredSize))
        {
            size = declaredSize;
        }

        var uploadedBy = context.Connection.ConnectedByUserId ?? Guid.Empty;
        if (uploadedBy == Guid.Empty)
        {
            logger.LogWarning("Skipping attachment {AttachmentId}: no ConnectedByUserId", attachmentId);
            await attachmentStorage.DeleteAsync(blobId, cancellationToken);
            return Result.Success;
        }

        var entity = new ProjectTaskAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = context.Connection.TenantId,
            ProjectId = context.Connection.ProjectId,
            TaskId = taskMapping.LocalEntityId,
            FileName = fileName.Length > 256 ? fileName[..256] : fileName,
            ContentType = contentType.Length > 128 ? contentType[..128] : contentType,
            FileSizeBytes = size,
            BlobId = blobId,
            UploadedById = uploadedBy,
        };
        await attachmentRepository.AddAsync(entity, cancellationToken);
        await mappingRepository.AddAsync(
            IntegrationMappingFactory.ForAttachment(
                context.Connection.TenantId,
                context.Connection.ProjectId,
                context.Connection.Id,
                entity.Id,
                attachmentId,
                $"{issueKey ?? issueId}:{attachmentId}",
                new { fileName, contentType, size }),
            cancellationToken);

        logger.LogInformation(
            "Imported Jira attachment {AttachmentId} onto task {TaskId}",
            attachmentId,
            taskMapping.LocalEntityId);
        return Result.Success;
    }

    private static bool TryReadAttachment(
        JsonElement payload,
        out string? attachmentId,
        out JsonElement attachmentEl)
    {
        attachmentId = null;
        attachmentEl = default;
        if (!payload.TryGetProperty("attachment", out attachmentEl)
            || !attachmentEl.TryGetProperty("id", out var idEl))
        {
            return false;
        }

        attachmentId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        return !string.IsNullOrWhiteSpace(attachmentId);
    }
}

public sealed class JiraAttachmentDeletedWebhookHandler(
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IIssueAttachmentStorageService attachmentStorage,
    ILogger<JiraAttachmentDeletedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.AttachmentDeleted;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        string? attachmentId = null;
        if (context.Payload.TryGetProperty("attachment", out var attachmentEl)
            && attachmentEl.TryGetProperty("id", out var idEl))
        {
            attachmentId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        }

        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            return Result.Success;
        }

        var mapping = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == context.Connection.Id
                && m.LocalEntityType == IntegrationLocalEntityTypes.TaskAttachment
                && m.ExternalEntityId == attachmentId,
            cancellationToken: cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var attachment = await attachmentRepository.GetAsync(
            filter: a => a.Id == mapping.LocalEntityId,
            cancellationToken: cancellationToken);
        await mappingRepository.DeleteAsync(mapping, cancellationToken);
        if (attachment is not null)
        {
            try
            {
                await attachmentStorage.DeleteAsync(attachment.BlobId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete blob {BlobId} for attachment {Id}", attachment.BlobId, attachment.Id);
            }

            await attachmentRepository.DeleteAsync(attachment, cancellationToken);
            logger.LogInformation(
                "Deleted local attachment {AttachmentId} after Jira attachment {JiraId} deletion",
                attachment.Id,
                attachmentId);
        }

        return Result.Success;
    }
}
