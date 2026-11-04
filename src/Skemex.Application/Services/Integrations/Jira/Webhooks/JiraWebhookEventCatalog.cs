using Skemex.Domain.Enums;

namespace Skemex.Application.Services.Integrations.Jira.Webhooks;

/// <summary>
/// Canonical Skemex webhook event keys + Jira <c>webhookEvent</c> names + flags.
/// </summary>
public static class JiraWebhookEventCatalog
{
    public const string IssueCreated = "issue.created";
    public const string IssueUpdated = "issue.updated";
    public const string IssueDeleted = "issue.deleted";
    public const string CommentCreated = "comment.created";
    public const string WorklogCreated = "worklog.created";
    public const string WorklogUpdated = "worklog.updated";
    public const string WorklogDeleted = "worklog.deleted";
    public const string AttachmentCreated = "attachment.created";
    public const string AttachmentDeleted = "attachment.deleted";
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    public const string UserDeleted = "user.deleted";

    public static IReadOnlyList<JiraWebhookEventDefinition> All { get; } =
    [
        new(IssueCreated, "Issue created", "jira:issue_created", JiraWebhookEventType.IssueCreated, true, true),
        new(IssueUpdated, "Issue updated", "jira:issue_updated", JiraWebhookEventType.IssueUpdated, true, true),
        new(IssueDeleted, "Issue deleted", "jira:issue_deleted", JiraWebhookEventType.IssueDeleted, true, true),
        new(CommentCreated, "Comment created", "comment_created", JiraWebhookEventType.CommentCreated, true, true),
        new(WorklogCreated, "Worklog created", "worklog_created", JiraWebhookEventType.WorklogCreated, true, false),
        new(WorklogUpdated, "Worklog updated", "worklog_updated", JiraWebhookEventType.WorklogUpdated, true, false),
        new(WorklogDeleted, "Worklog deleted", "worklog_deleted", JiraWebhookEventType.WorklogDeleted, true, false),
        new(AttachmentCreated, "Attachment created", "attachment_created", JiraWebhookEventType.AttachmentCreated, true, false),
        new(AttachmentDeleted, "Attachment deleted", "attachment_deleted", JiraWebhookEventType.AttachmentDeleted, true, false),
        new(UserCreated, "User created", "user_created", JiraWebhookEventType.UserCreated, true, false),
        new(UserUpdated, "User updated", "user_updated", JiraWebhookEventType.UserUpdated, true, false),
        new(UserDeleted, "User deleted", "user_deleted", JiraWebhookEventType.UserDeleted, true, false),
    ];

    private static readonly Dictionary<string, JiraWebhookEventDefinition> ByKey =
        All.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, JiraWebhookEventDefinition> ByJiraEvent =
        All.ToDictionary(e => e.JiraEventName, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> OAuthDynamicJiraEventNames { get; } =
        All.Where(e => e.OAuthDynamicSupported)
            .Select(e => e.JiraEventName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static JiraWebhookEventType DefaultEnabledFlags { get; } =
        All.Where(e => e.DefaultEnabled).Aggregate(JiraWebhookEventType.None, (acc, e) => acc | e.Flag);

    public static JiraWebhookEventType OAuthDynamicFlags { get; } = JiraWebhookEventType.OAuthDynamic;

    public static JiraWebhookEventDefinition? FindByKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out var def) ? def : null;

    public static JiraWebhookEventDefinition? FindByJiraEvent(string? jiraEvent) =>
        !string.IsNullOrWhiteSpace(jiraEvent) && ByJiraEvent.TryGetValue(jiraEvent.Trim(), out var def)
            ? def
            : null;

    public static JiraWebhookEventType FlagsFromEnabledMap(IReadOnlyDictionary<string, bool> enabledByKey)
    {
        var flags = JiraWebhookEventType.None;
        foreach (var def in All)
        {
            if (enabledByKey.TryGetValue(def.Key, out var enabled) ? enabled : def.DefaultEnabled)
            {
                flags |= def.Flag;
            }
        }

        return flags;
    }

    public static Dictionary<string, bool> EnabledMapFromFlags(JiraWebhookEventType enabled)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in All)
        {
            map[def.Key] = enabled.HasFlag(def.Flag);
        }

        return map;
    }
}

public sealed record JiraWebhookEventDefinition(
    string Key,
    string DisplayName,
    string JiraEventName,
    JiraWebhookEventType Flag,
    bool DefaultEnabled,
    bool OAuthDynamicSupported);
