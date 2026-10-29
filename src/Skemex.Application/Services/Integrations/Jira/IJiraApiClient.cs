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
}
