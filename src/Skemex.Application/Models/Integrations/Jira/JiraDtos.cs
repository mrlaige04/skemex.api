using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Integrations.Jira;

public sealed class JiraAuthorizeStartDto
{
    public required string AuthorizationUrl { get; init; }
}

public sealed class JiraConnectionDto
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CloudId { get; init; }
    public string? SiteUrl { get; init; }
    public string? SiteName { get; init; }
    public string? ExternalProjectId { get; init; }
    public string? ExternalProjectKey { get; init; }
    public string? ExternalProjectName { get; init; }
    public string? LastError { get; init; }
    public bool WebhookRegistered { get; init; }
    /// <summary>Full inbound URL (includes secret). Configure manually in Jira if auto-register failed.</summary>
    public string? WebhookCallbackUrl { get; init; }
    /// <summary>
    /// True when the callback host is localhost/loopback — Jira Cloud cannot deliver to it without a tunnel.
    /// </summary>
    public bool WebhookUrlNotPubliclyReachable { get; init; }
    public DateTimeOffset? WebhookRegisteredAt { get; init; }
    public IReadOnlyList<JiraWebhookEventSettingDto> WebhookEvents { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class JiraWebhookEventSettingDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string JiraEventName { get; init; } = string.Empty;
    public bool Enabled { get; init; }
}

public sealed class UpdateJiraWebhookSettingsRequest
{
    /// <summary>Map of event key → enabled.</summary>
    public Dictionary<string, bool> Events { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class JiraRegisterWebhookRequest
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string ProjectKey { get; init; } = string.Empty;
    public IReadOnlyList<string> Events { get; init; } = [];
}

public sealed class JiraRegisteredWebhookDto
{
    public string Id { get; init; } = string.Empty;
}

/// <summary>Wrapper so <see cref="ErrorOr"/> can represent “no connection yet” without a null value.</summary>
public sealed class JiraConnectionResponseDto
{
    public JiraConnectionDto? Connection { get; init; }
}

public sealed class JiraAccessibleResourceDto
{
    public string Id { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
}

public sealed class JiraProjectDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ProjectTypeKey { get; init; }
}

public sealed class JiraTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
    public string? Scope { get; init; }
    public string TokenType { get; init; } = "Bearer";
}

public sealed class MapJiraProjectRequest
{
    public string CloudId { get; set; } = string.Empty;
    public string? SiteUrl { get; set; }
    public string? SiteName { get; set; }
    public string ExternalProjectId { get; set; } = string.Empty;
    public string ExternalProjectKey { get; set; } = string.Empty;
    public string ExternalProjectName { get; set; } = string.Empty;
}

public enum JiraExportScope
{
    AllFiltered = 0,
    Selected = 1,
    All = 2,
}

public sealed class ExportTasksToJiraRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JiraExportScope Scope { get; set; } = JiraExportScope.AllFiltered;
    public List<Guid>? SelectedTaskIds { get; set; }
    public string? Search { get; set; }
    public Guid? ColumnId { get; set; }
    public Guid? AssigneeId { get; set; }
    public bool Unassigned { get; set; }
    public bool SkipAlreadyExported { get; set; } = true;
}

public sealed class ExportTasksToJiraResultDto
{
    public int TotalCandidates { get; init; }
    public int Exported { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<ExportTasksToJiraItemResultDto> Items { get; init; } = [];
}

public sealed class ExportTasksToJiraItemResultDto
{
    public Guid TaskId { get; init; }
    public string TaskCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? JiraIssueKey { get; init; }
    public string? Message { get; init; }
}

public sealed class JiraCreatedIssueDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
}

/// <summary>Lightweight issue lookup used while building parent/child trees on export.</summary>
public sealed class JiraIssueRefDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string IssueTypeName { get; init; } = string.Empty;
    public bool Subtask { get; init; }

    public bool IsEpic =>
        string.Equals(IssueTypeName, "Epic", StringComparison.OrdinalIgnoreCase)
        || string.Equals(IssueTypeName, "Feature", StringComparison.OrdinalIgnoreCase);
}

public sealed class JiraIssueTypeDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Subtask { get; init; }
}

public sealed class JiraUserDto
{
    public string AccountId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? EmailAddress { get; init; }
    public bool Active { get; init; } = true;
    public string? AvatarUrl { get; init; }
}

public sealed class JiraCreateIssueRequest
{
    public required string ProjectKey { get; init; }
    public required string Summary { get; init; }
    public required string IssueTypeName { get; init; }
    public string? IssueTypeId { get; init; }
    public string? Description { get; init; }
    public string? AssigneeAccountId { get; init; }
    /// <summary>Jira parent issue key (fallback when id is unavailable).</summary>
    public string? ParentIssueKey { get; init; }
    /// <summary>Jira parent issue id — preferred for Epic/Story hierarchy via the parent field.</summary>
    public string? ParentIssueId { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public string? OriginalEstimate { get; init; }
    public string? RemainingEstimate { get; init; }
}

public sealed class JiraAddWorklogRequest
{
    public required string Started { get; init; }
    public required int TimeSpentSeconds { get; init; }
    public string? Comment { get; init; }
}

public sealed class JiraAddCommentRequest
{
    public required string Body { get; init; }
}

public sealed class JiraCreatedCommentDto
{
    public string Id { get; init; } = string.Empty;
}

public sealed class JiraCreatedWorklogDto
{
    public string Id { get; init; } = string.Empty;
}

public sealed class JiraRemoteIssueDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string IssueType { get; init; } = "Task";
    public string? Status { get; init; }
    public string? AssigneeAccountId { get; init; }
    public string? AssigneeEmail { get; init; }
    public string? AssigneeDisplayName { get; init; }
    public string? ParentKey { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public int? OriginalEstimateMinutes { get; init; }
    public int? RemainingEstimateMinutes { get; init; }
    public int? TimeSpentMinutes { get; init; }
    public IReadOnlyList<JiraRemoteWorklogDto> Worklogs { get; init; } = [];
}

public sealed class JiraRemoteWorklogDto
{
    public string? Id { get; init; }
    public DateTime StartedAt { get; init; }
    public int SpentMinutes { get; init; }
    public string? Comment { get; init; }
    public string? AuthorEmail { get; init; }
}

public sealed class JiraRemoteCommentDto
{
    public string Id { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? AuthorEmail { get; init; }
    public string? AuthorDisplayName { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class JiraIssuePreviewDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string IssueType { get; init; } = string.Empty;
    public string? Status { get; init; }
    public string? AssigneeDisplayName { get; init; }
    public string? ParentKey { get; init; }
    public bool AlreadyImported { get; init; }
    public string? LocalTaskCode { get; init; }
}

public sealed class ImportTasksFromJiraRequest
{
    /// <summary>When true, import every not-yet-imported issue from the mapped Jira project.</summary>
    public bool ImportAll { get; set; }

    /// <summary>Jira issue keys to import when <see cref="ImportAll"/> is false.</summary>
    public List<string>? IssueKeys { get; set; }

    public bool SkipAlreadyImported { get; set; } = true;
}

public sealed class ImportTasksFromJiraResultDto
{
    public int TotalCandidates { get; init; }
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<ImportTasksFromJiraItemResultDto> Items { get; init; } = [];
}

public sealed class ImportTasksFromJiraItemResultDto
{
    public string JiraIssueKey { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? LocalTaskCode { get; init; }
    public Guid? LocalTaskId { get; init; }
    public string? Message { get; init; }
}

public sealed class JiraEntityMappingWorkspaceDto
{
    public string LocalEntityType { get; init; } = string.Empty;
    public string ExternalEntityType { get; init; } = string.Empty;
    public IReadOnlyList<JiraMappingLocalItemDto> LocalItems { get; init; } = [];
    public IReadOnlyList<JiraMappingRemoteItemDto> RemoteItems { get; init; } = [];
}

public sealed class JiraMappingLocalItemDto
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? SecondaryText { get; init; }
    public Guid? ParentTaskId { get; init; }
    public string? ParentTaskCode { get; init; }
    public JiraMappingLinkDto? Mapping { get; init; }
}

public sealed class JiraMappingRemoteItemDto
{
    public string Id { get; init; } = string.Empty;
    public string? Key { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? SecondaryText { get; init; }
    public Guid? MappedLocalEntityId { get; init; }
}

public sealed class JiraMappingLinkDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
    public string ExternalEntityId { get; init; } = string.Empty;
    public string? ExternalEntityKey { get; init; }
    public DateTimeOffset? LastSyncedAt { get; init; }
}

public sealed class UpsertJiraEntityMappingRequest
{
    public string LocalEntityType { get; set; } = string.Empty;
    public Guid LocalEntityId { get; set; }
    public string ExternalEntityId { get; set; } = string.Empty;
    public string? ExternalEntityKey { get; set; }
    public string? Status { get; set; }
}

public sealed class UpsertJiraEntityMappingResultDto
{
    public JiraMappingLinkDto Mapping { get; init; } = null!;
}
