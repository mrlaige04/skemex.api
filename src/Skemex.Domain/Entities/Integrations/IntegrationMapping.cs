using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Integrations;

/// <summary>
/// Links a local Skemex entity to an external entity under one integration/provider.
/// Per provider, a local entity has at most one mapping (e.g. one Jira issue per ProjectTask).
/// </summary>
public class IntegrationMapping : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid ProjectIntegrationId { get; set; }
    public ProjectIntegration ProjectIntegration { get; set; } = null!;

    public IntegrationProvider Provider { get; set; } = IntegrationProvider.Jira;

    public IntegrationMappingStatus Status { get; set; } = IntegrationMappingStatus.Active;

    /// <summary>Local entity kind, e.g. <c>Task</c>, <c>TaskComment</c>.</summary>
    public string LocalEntityType { get; set; } = IntegrationLocalEntityTypes.Task;

    public Guid LocalEntityId { get; set; }

    /// <summary>External entity kind, e.g. <c>Issue</c>, <c>Comment</c>.</summary>
    public string ExternalEntityType { get; set; } = IntegrationExternalEntityTypes.Issue;

    public string ExternalEntityId { get; set; } = string.Empty;

    public string? ExternalEntityKey { get; set; }

    /// <summary>Provider-specific extras (accountId, mimeType, raw ids, …) as JSON object.</summary>
    public string? ExternalDataJson { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public Guid? TaskId { get; set; }
    public ProjectTask? Task { get; set; }

    public Guid? TaskCommentId { get; set; }
    public ProjectTaskComment? TaskComment { get; set; }

    public Guid? TaskWorkLogId { get; set; }
    public ProjectTaskWorkLog? TaskWorkLog { get; set; }

    public Guid? TaskAttachmentId { get; set; }
    public ProjectTaskAttachment? TaskAttachment { get; set; }

    public Guid? ProjectUserId { get; set; }
    public ProjectUser? ProjectUser { get; set; }
}

public static class IntegrationLocalEntityTypes
{
    public const string Task = "Task";
    public const string TaskComment = "TaskComment";
    public const string TaskWorkLog = "TaskWorkLog";
    public const string TaskAttachment = "TaskAttachment";
    public const string ProjectUser = "ProjectUser";
}

public static class IntegrationExternalEntityTypes
{
    public const string Issue = "Issue";
    public const string Comment = "Comment";
    public const string Worklog = "Worklog";
    public const string Attachment = "Attachment";
    public const string User = "User";
}
