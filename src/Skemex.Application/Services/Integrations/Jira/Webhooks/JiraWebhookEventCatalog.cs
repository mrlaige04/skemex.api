namespace Skemex.Application.Services.Integrations.Jira.Webhooks;

/// <summary>
/// Canonical Skemex webhook event keys + Jira <c>webhookEvent</c> names.
/// Jira registration always subscribes to the full set; enable/disable is local only.
/// </summary>
public static class JiraWebhookEventCatalog
{
    public const string IssueCreated = "issue.created";
    public const string IssueUpdated = "issue.updated";
    public const string IssueDeleted = "issue.deleted";
    public const string CommentCreated = "comment.created";
    public const string CommentUpdated = "comment.updated";
    public const string CommentDeleted = "comment.deleted";
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
        // OAuth 3LO dynamic webhook API supports issue + comment events only.
        new(IssueCreated, "Issue created", "jira:issue_created", DefaultEnabled: true, OAuthDynamicSupported: true),
        new(IssueUpdated, "Issue updated", "jira:issue_updated", DefaultEnabled: true, OAuthDynamicSupported: true),
        new(IssueDeleted, "Issue deleted", "jira:issue_deleted", DefaultEnabled: true, OAuthDynamicSupported: true),
        new(CommentCreated, "Comment created", "comment_created", DefaultEnabled: true, OAuthDynamicSupported: true),
        new(CommentUpdated, "Comment updated", "comment_updated", DefaultEnabled: true, OAuthDynamicSupported: true),
        new(CommentDeleted, "Comment deleted", "comment_deleted", DefaultEnabled: true, OAuthDynamicSupported: true),
        // Worklog / attachment / user are admin-webhook (or Connect) only — not registerable via OAuth dynamic API.
        new(WorklogCreated, "Worklog created", "worklog_created", DefaultEnabled: true, OAuthDynamicSupported: false),
        new(WorklogUpdated, "Worklog updated", "worklog_updated", DefaultEnabled: true, OAuthDynamicSupported: false),
        new(WorklogDeleted, "Worklog deleted", "worklog_deleted", DefaultEnabled: true, OAuthDynamicSupported: false),
        new(AttachmentCreated, "Attachment created", "attachment_created", DefaultEnabled: false, OAuthDynamicSupported: false),
        new(AttachmentDeleted, "Attachment deleted", "attachment_deleted", DefaultEnabled: false, OAuthDynamicSupported: false),
        new(UserCreated, "User created", "user_created", DefaultEnabled: false, OAuthDynamicSupported: false),
        new(UserUpdated, "User updated", "user_updated", DefaultEnabled: false, OAuthDynamicSupported: false),
        new(UserDeleted, "User deleted", "user_deleted", DefaultEnabled: false, OAuthDynamicSupported: false),
    ];

    private static readonly Dictionary<string, JiraWebhookEventDefinition> ByKey =
        All.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, JiraWebhookEventDefinition> ByJiraEvent =
        All.ToDictionary(e => e.JiraEventName, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AllJiraEventNames { get; } =
        All.Select(e => e.JiraEventName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Events Jira accepts on <c>POST /rest/api/3/webhook</c> for OAuth 2.0 (3LO) apps.
    /// </summary>
    public static IReadOnlyList<string> OAuthDynamicJiraEventNames { get; } =
        All.Where(e => e.OAuthDynamicSupported)
            .Select(e => e.JiraEventName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static JiraWebhookEventDefinition? FindByKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key.Trim(), out var def) ? def : null;

    public static JiraWebhookEventDefinition? FindByJiraEvent(string? jiraEvent) =>
        !string.IsNullOrWhiteSpace(jiraEvent) && ByJiraEvent.TryGetValue(jiraEvent.Trim(), out var def)
            ? def
            : null;

    public static Dictionary<string, bool> CreateDefaultSettings() =>
        All.ToDictionary(e => e.Key, e => e.DefaultEnabled, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, bool> MergeWithDefaults(IReadOnlyDictionary<string, bool>? stored)
    {
        var merged = CreateDefaultSettings();
        if (stored is null)
        {
            return merged;
        }

        foreach (var (key, enabled) in stored)
        {
            if (merged.ContainsKey(key))
            {
                merged[key] = enabled;
            }
        }

        return merged;
    }
}

public sealed record JiraWebhookEventDefinition(
    string Key,
    string DisplayName,
    string JiraEventName,
    bool DefaultEnabled,
    bool OAuthDynamicSupported);
