namespace Skemex.Domain.Enums;

/// <summary>
/// Flagged set of Jira webhook event kinds a single <c>IntegrationWebhook</c> may subscribe to / process.
/// </summary>
[Flags]
public enum JiraWebhookEventType : long
{
    None = 0,
    IssueCreated = 1L << 0,
    IssueUpdated = 1L << 1,
    IssueDeleted = 1L << 2,
    CommentCreated = 1L << 3,
    CommentUpdated = 1L << 4,
    CommentDeleted = 1L << 5,
    WorklogCreated = 1L << 6,
    WorklogUpdated = 1L << 7,
    WorklogDeleted = 1L << 8,
    AttachmentCreated = 1L << 9,
    AttachmentDeleted = 1L << 10,
    UserCreated = 1L << 11,
    UserUpdated = 1L << 12,
    UserDeleted = 1L << 13,

    Issues = IssueCreated | IssueUpdated | IssueDeleted,
    /// <summary>Comment create only (update/delete not processed).</summary>
    Comments = CommentCreated,
    Worklogs = WorklogCreated | WorklogUpdated | WorklogDeleted,
    Attachments = AttachmentCreated | AttachmentDeleted,
    Users = UserCreated | UserUpdated | UserDeleted,

    /// <summary>Events the OAuth dynamic webhook API can register.</summary>
    OAuthDynamic = Issues | Comments,

    /// <summary>All known Jira event types Skemex supports.</summary>
    All = OAuthDynamic | Worklogs | Attachments | Users,
}
