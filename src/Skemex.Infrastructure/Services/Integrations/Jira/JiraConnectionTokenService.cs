using ErrorOr;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed class JiraConnectionTokenService(
    IJiraApiClient jiraApiClient,
    IEncryptService encryptService,
    ITenantRepository<IntegrationConnection> connectionRepository) : IJiraConnectionTokenService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    public async Task<ErrorOr<string>> GetValidAccessTokenAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connection.EncryptedAccessToken))
        {
            return Error.Failure("Jira.NoAccessToken", "Jira connection has no access token.");
        }

        var accessToken = encryptService.Unprotect(connection.EncryptedAccessToken);
        var expiresAt = connection.AccessTokenExpiresAt;
        if (expiresAt is null || expiresAt > DateTimeOffset.UtcNow.Add(RefreshSkew))
        {
            return accessToken;
        }

        if (string.IsNullOrWhiteSpace(connection.EncryptedRefreshToken))
        {
            return Error.Failure(
                "Jira.RefreshRequired",
                "Jira access token expired and no refresh token is available. Reconnect Jira.");
        }

        try
        {
            var refreshToken = encryptService.Unprotect(connection.EncryptedRefreshToken);
            var tokens = await jiraApiClient.RefreshAccessTokenAsync(refreshToken, cancellationToken);

            connection.EncryptedAccessToken = encryptService.Protect(tokens.AccessToken);
            if (!string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                connection.EncryptedRefreshToken = encryptService.Protect(tokens.RefreshToken);
            }

            connection.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                Math.Max(tokens.ExpiresIn, 60));
            if (!string.IsNullOrWhiteSpace(tokens.Scope))
            {
                connection.Scopes = tokens.Scope;
            }

            connection.LastError = null;
            await connectionRepository.UpdateAsync(connection, cancellationToken);
            return tokens.AccessToken;
        }
        catch (Exception ex)
        {
            connection.LastError = ex.Message;
            await connectionRepository.UpdateAsync(connection, cancellationToken);
            return Error.Failure("Jira.TokenRefreshFailed", "Failed to refresh Jira access token.");
        }
    }
}
