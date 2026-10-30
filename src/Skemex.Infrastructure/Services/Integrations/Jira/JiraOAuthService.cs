using System.Security.Cryptography;
using ErrorOr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication.External;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed class JiraOAuthService(
    IOptions<JiraOptions> jiraOptions,
    IOptions<ExternalAuthOptions> externalAuthOptions,
    IOptions<AppOptions> appOptions,
    IMemoryCache cache,
    IJiraApiClient jiraApiClient,
    IEncryptService encryptService,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<IntegrationConnection> connectionRepository,
    ILogger<JiraOAuthService> logger) : IJiraOAuthService
{
    public const string AuthorizeScopes =
        "read:jira-work write:jira-work read:jira-user offline_access";

    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(15);

    private readonly JiraOptions _jira = jiraOptions.Value;

    public bool IsConfigured => _jira.IsConfigured;

    public async Task<ErrorOr<JiraAuthorizeStartDto>> StartAuthorizationAsync(
        Guid projectId,
        Guid userId,
        Guid tenantId,
        string projectCode,
        CancellationToken cancellationToken = default)
    {
        if (!_jira.IsConfigured)
        {
            return Error.Failure(
                "Jira.NotConfigured",
                "Jira OAuth is not configured. Set Jira__ClientId and Jira__ClientSecret.");
        }

        var redirectUri = ResolveRedirectUri();
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return Error.Failure(
                "Jira.RedirectUriMissing",
                "Configure Jira:RedirectUri or Auth:External:CallbackBaseUrl for the OAuth callback.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: p => p.Id == projectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var state = CreateOpaqueToken();
        cache.Set(
            StateCacheKey(state),
            new JiraOAuthState(projectId, userId, tenantId, projectCode.Trim(), DateTimeOffset.UtcNow),
            StateTtl);

        var authorizeUrl =
            "https://auth.atlassian.com/authorize"
            + "?audience=api.atlassian.com"
            + $"&client_id={Uri.EscapeDataString(_jira.ClientId)}"
            + $"&scope={Uri.EscapeDataString(AuthorizeScopes)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&state=" + Uri.EscapeDataString(state)
            + "&response_type=code"
            + "&prompt=consent";

        return new JiraAuthorizeStartDto { AuthorizationUrl = authorizeUrl };
    }

    public async Task<Uri> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default)
    {
        var frontendBase = appOptions.Value.FrontendBaseUrl.Trim().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(error))
        {
            var message = string.IsNullOrWhiteSpace(errorDescription) ? error : errorDescription;
            return FrontendResult(frontendBase, null, success: false, message!);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return FrontendResult(frontendBase, null, success: false, "Missing authorization code or state.");
        }

        if (!cache.TryGetValue(StateCacheKey(state), out JiraOAuthState? oauthState)
            || oauthState is null)
        {
            return FrontendResult(
                frontendBase,
                null,
                success: false,
                "Invalid or expired OAuth state. Please try again.");
        }

        cache.Remove(StateCacheKey(state));

        var redirectUri = ResolveRedirectUri();
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return FrontendResult(
                frontendBase,
                oauthState.ProjectCode,
                success: false,
                "Jira redirect URI is not configured.");
        }

        try
        {
            var tokens = await jiraApiClient.ExchangeAuthorizationCodeAsync(
                code,
                redirectUri,
                cancellationToken);

            var resources = await jiraApiClient.GetAccessibleResourcesAsync(
                tokens.AccessToken,
                cancellationToken);

            var site = resources.FirstOrDefault();
            if (site is null)
            {
                return FrontendResult(
                    frontendBase,
                    oauthState.ProjectCode,
                    success: false,
                    "No Atlassian Jira sites are accessible with this account.");
            }

            var connection = await connectionRepository.GetAsync(
                filter: c =>
                    c.ProjectId == oauthState.ProjectId
                    && c.Provider == IntegrationProvider.Jira,
                cancellationToken: cancellationToken);

            var isNew = connection is null;
            connection ??= new IntegrationConnection
            {
                Id = Guid.NewGuid(),
                TenantId = oauthState.TenantId,
                ProjectId = oauthState.ProjectId,
                Provider = IntegrationProvider.Jira,
            };

            connection.Status = IntegrationConnectionStatus.PendingProjectMapping;
            connection.CloudId = site.Id;
            connection.SiteUrl = site.Url;
            connection.SiteName = site.Name;
            connection.EncryptedAccessToken = encryptService.Protect(tokens.AccessToken);
            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                connection.EncryptedRefreshToken = encryptService.Protect(tokens.RefreshToken);
            }

            connection.AccessTokenExpiresAt =
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(tokens.ExpiresIn, 60));
            connection.Scopes = tokens.Scope ?? AuthorizeScopes;
            connection.ConnectedByUserId = oauthState.UserId;
            connection.LastError = null;
            connection.ExternalProjectId = null;
            connection.ExternalProjectKey = null;
            connection.ExternalProjectName = null;

            if (isNew)
            {
                await connectionRepository.AddAsync(connection, cancellationToken);
            }
            else
            {
                await connectionRepository.UpdateAsync(connection, cancellationToken);
            }

            return FrontendResult(frontendBase, oauthState.ProjectCode, success: true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jira OAuth callback failed for project {ProjectId}", oauthState.ProjectId);
            return FrontendResult(
                frontendBase,
                oauthState.ProjectCode,
                success: false,
                "Failed to complete Jira authorization.");
        }
    }

    private string? ResolveRedirectUri()
    {
        if (!string.IsNullOrWhiteSpace(_jira.RedirectUri))
        {
            return _jira.RedirectUri.Trim();
        }

        var callbackBase = externalAuthOptions.Value.CallbackBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(callbackBase))
        {
            return null;
        }

        return $"{callbackBase}/api/integrations/jira/callback";
    }

    private static Uri FrontendResult(
        string frontendBase,
        string? projectCode,
        bool success,
        string? message)
    {
        var path = string.IsNullOrWhiteSpace(projectCode)
            ? "/"
            : $"/{Uri.EscapeDataString(projectCode.Trim())}/settings";

        var parts = new List<string>
        {
            "tab=integrations",
            $"jira={(success ? "connected" : "error")}",
        };
        if (!string.IsNullOrWhiteSpace(message))
        {
            parts.Add($"jiraMessage={Uri.EscapeDataString(message)}");
        }

        return new Uri($"{frontendBase}{path}?{string.Join('&', parts)}");
    }

    private static string StateCacheKey(string state) => $"jira-oauth-state:{state}";

    private static string CreateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record JiraOAuthState(
        Guid ProjectId,
        Guid UserId,
        Guid TenantId,
        string ProjectCode,
        DateTimeOffset CreatedAt);
}
