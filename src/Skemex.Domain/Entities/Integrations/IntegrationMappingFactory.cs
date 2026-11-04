using System.Text.Json;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Integrations;

public static class IntegrationMappingFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static IntegrationMapping ForTask(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        Guid taskId,
        string externalIssueId,
        string? externalIssueKey,
        object? externalData = null) =>
        BindLocal(
            Base(tenantId, projectId, integrationId, IntegrationLocalEntityTypes.Task, taskId,
                IntegrationExternalEntityTypes.Issue, externalIssueId, externalIssueKey, externalData),
            taskId: taskId);

    public static IntegrationMapping ForComment(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        Guid commentId,
        string externalCommentId,
        string? externalKey,
        object? externalData = null) =>
        BindLocal(
            Base(tenantId, projectId, integrationId, IntegrationLocalEntityTypes.TaskComment, commentId,
                IntegrationExternalEntityTypes.Comment, externalCommentId, externalKey, externalData),
            taskCommentId: commentId);

    public static IntegrationMapping ForWorkLog(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        Guid workLogId,
        string externalWorklogId,
        string? externalKey,
        object? externalData = null) =>
        BindLocal(
            Base(tenantId, projectId, integrationId, IntegrationLocalEntityTypes.TaskWorkLog, workLogId,
                IntegrationExternalEntityTypes.Worklog, externalWorklogId, externalKey, externalData),
            taskWorkLogId: workLogId);

    public static IntegrationMapping ForAttachment(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        Guid attachmentId,
        string externalAttachmentId,
        string? externalKey,
        object? externalData = null) =>
        BindLocal(
            Base(tenantId, projectId, integrationId, IntegrationLocalEntityTypes.TaskAttachment, attachmentId,
                IntegrationExternalEntityTypes.Attachment, externalAttachmentId, externalKey, externalData),
            taskAttachmentId: attachmentId);

    public static IntegrationMapping ForProjectUser(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        Guid projectUserId,
        string externalUserId,
        string? externalKey,
        object? externalData = null) =>
        BindLocal(
            Base(tenantId, projectId, integrationId, IntegrationLocalEntityTypes.ProjectUser, projectUserId,
                IntegrationExternalEntityTypes.User, externalUserId, externalKey, externalData),
            projectUserId: projectUserId);

    public static string? SerializeExternalData(object? data) =>
        data is null ? null : JsonSerializer.Serialize(data, JsonOptions);

    private static IntegrationMapping Base(
        Guid tenantId,
        Guid projectId,
        Guid integrationId,
        string localType,
        Guid localId,
        string externalType,
        string externalId,
        string? externalKey,
        object? externalData) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectIntegrationId = integrationId,
            Provider = IntegrationProvider.Jira,
            Status = IntegrationMappingStatus.Active,
            LocalEntityType = localType,
            LocalEntityId = localId,
            ExternalEntityType = externalType,
            ExternalEntityId = externalId,
            ExternalEntityKey = externalKey,
            ExternalDataJson = SerializeExternalData(externalData),
            LastSyncedAt = DateTimeOffset.UtcNow,
        };

    private static IntegrationMapping BindLocal(
        IntegrationMapping mapping,
        Guid? taskId = null,
        Guid? taskCommentId = null,
        Guid? taskWorkLogId = null,
        Guid? taskAttachmentId = null,
        Guid? projectUserId = null)
    {
        mapping.TaskId = taskId;
        mapping.TaskCommentId = taskCommentId;
        mapping.TaskWorkLogId = taskWorkLogId;
        mapping.TaskAttachmentId = taskAttachmentId;
        mapping.ProjectUserId = projectUserId;
        return mapping;
    }
}
