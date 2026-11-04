using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Services.Integrations.Jira;

/// <summary>Jira Cloud REST API v2 / Atlassian OAuth resource client.</summary>
public interface IJiraApiClient
{
    Task<IReadOnlyList<JiraAccessibleResourceDto>> GetAccessibleResourcesAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraProjectDto>> GetProjectsAsync(
        string accessToken,
        string cloudId,
        CancellationToken cancellationToken = default);

    Task<JiraTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);

    Task<JiraTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<JiraUserDto?> FindUserByEmailAsync(
        string accessToken,
        string cloudId,
        string email,
        CancellationToken cancellationToken = default);

    Task<JiraUserDto?> FindUserByDisplayNameAsync(
        string accessToken,
        string cloudId,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>Lists users assignable to issues in the given Jira project.</summary>
    Task<IReadOnlyList<JiraUserDto>> GetAssignableUsersAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults = 200,
        CancellationToken cancellationToken = default);

    /// <summary>Issue types available when creating issues in the given project.</summary>
    Task<IReadOnlyList<JiraIssueTypeDto>> GetProjectIssueTypesAsync(
        string accessToken,
        string cloudId,
        string projectKeyOrId,
        CancellationToken cancellationToken = default);

    Task<JiraCreatedIssueDto> CreateIssueAsync(
        string accessToken,
        string cloudId,
        JiraCreateIssueRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the issue or <c>null</c> when it is missing (404).</summary>
    Task<JiraIssueRefDto?> TryGetIssueAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a full issue payload used for import/webhook sync.</summary>
    Task<JiraRemoteIssueDto?> GetRemoteIssueAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an OAuth dynamic webhook via <c>POST /rest/api/3/webhook</c>
    /// (requires <c>manage:jira-webhook</c>).
    /// </summary>
    Task<JiraRegisteredWebhookDto> RegisterWebhookAsync(
        string accessToken,
        string cloudId,
        JiraRegisterWebhookRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteWebhookAsync(
        string accessToken,
        string cloudId,
        string webhookId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends dynamic webhook life by 30 days (<c>PUT /rest/api/3/webhook/refresh</c>).
    /// </summary>
    Task RefreshWebhookAsync(
        string accessToken,
        string cloudId,
        string webhookId,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads attachment binary content by Jira attachment id.</summary>
    Task<(Stream Content, string ContentType, string? FileName)?> DownloadAttachmentAsync(
        string accessToken,
        string cloudId,
        string attachmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the parent of an existing issue by parent issue key
    /// (<c>fields.parent.key</c>), matching Jira Cloud create/update docs.
    /// </summary>
    Task UpdateIssueParentAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        string parentIssueKey,
        CancellationToken cancellationToken = default);

    Task<JiraCreatedWorklogDto> AddWorklogAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        JiraAddWorklogRequest request,
        CancellationToken cancellationToken = default);

    Task<JiraCreatedCommentDto> AddIssueCommentAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        JiraAddCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraRemoteCommentDto>> GetIssueCommentsAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default);

    /// <summary>Lists issues in a Jira project via JQL search (paginated until exhausted or max).</summary>
    Task<IReadOnlyList<JiraRemoteIssueDto>> SearchProjectIssuesAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults = 200,
        CancellationToken cancellationToken = default);
}
