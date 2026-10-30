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
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
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
    public string? Description { get; init; }
    public string? AssigneeAccountId { get; init; }
    public string? ParentIssueKey { get; init; }
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

public sealed class JiraRemoteIssueDto
{
    public string Id { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string IssueType { get; init; } = "Task";
    public string? Status { get; init; }
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
    public DateTime StartedAt { get; init; }
    public int SpentMinutes { get; init; }
    public string? Comment { get; init; }
    public string? AuthorEmail { get; init; }
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
