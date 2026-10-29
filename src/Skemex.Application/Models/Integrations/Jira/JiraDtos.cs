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
