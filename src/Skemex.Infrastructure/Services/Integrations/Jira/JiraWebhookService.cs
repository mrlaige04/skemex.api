using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication.External;
using Skemex.Infrastructure.Services;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

/// <summary>
/// One Jira webhook registration covers all catalog events. Enable/disable toggles only
/// affect local processing so settings changes never require re-registering in Jira.
/// </summary>
public sealed class JiraWebhookService(
    IOptions<JiraOptions> jiraOptions,
    IOptions<ExternalAuthOptions> externalAuthOptions,
    ITenantRepository<IntegrationConnection> connectionRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient,
    IEnumerable<IJiraWebhookEventHandler> eventHandlers,
    ILogger<JiraWebhookService> logger) : IJiraWebhookService
{
    private readonly Dictionary<string, IJiraWebhookEventHandler> _handlers = eventHandlers
        .GroupBy(h => h.EventKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public async Task EnsureRegisteredAsync(
        IntegrationConnection connection,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (connection.Status != IntegrationConnectionStatus.Connected
            || string.IsNullOrWhiteSpace(connection.CloudId)
            || string.IsNullOrWhiteSpace(connection.ExternalProjectKey))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(connection.WebhookSecret))
        {
            connection.WebhookSecret = CreateSecret();
        }

        // First time we persist per-event settings → previous registration may have subscribed
        // only to issue events; drop it so we re-register with the full catalog once.
        var upgradingToMultiEvent = string.IsNullOrWhiteSpace(connection.WebhookEventSettingsJson);
        JiraWebhookEventSettingsHelper.EnsureDefaults(connection);

        var callbackUrl = BuildCallbackUrl(connection);
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            logger.LogWarning(
                "Cannot register Jira webhook for connection {ConnectionId}: public API base URL is not configured.",
                connection.Id);
            connection.LastError =
                "Webhook URL could not be built. Set Jira:PublicApiBaseUrl or Auth:External:CallbackBaseUrl to a public HTTPS origin.";
            await connectionRepository.UpdateAsync(connection, cancellationToken);
            return;
        }

        if (upgradingToMultiEvent && !string.IsNullOrWhiteSpace(connection.WebhookId))
        {
            try
            {
                await jiraApiClient.DeleteWebhookAsync(
                    accessToken,
                    connection.CloudId!,
                    connection.WebhookId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete legacy Jira webhook {WebhookId} before multi-event re-register",
                    connection.WebhookId);
            }

            connection.WebhookId = null;
            connection.WebhookRegisteredAt = null;
        }

        if (!string.IsNullOrWhiteSpace(connection.WebhookId))
        {
            // Dynamic OAuth webhooks expire after 30 days — refresh on each ensure.
            try
            {
                await jiraApiClient.RefreshWebhookAsync(
                    accessToken,
                    connection.CloudId!,
                    connection.WebhookId,
                    cancellationToken);
                connection.WebhookRegisteredAt = DateTimeOffset.UtcNow;
                connection.LastError = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to refresh Jira webhook {WebhookId}; will try re-register on next mapping",
                    connection.WebhookId);
            }

            await connectionRepository.UpdateAsync(connection, cancellationToken);
            return;
        }

        try
        {
            var registered = await jiraApiClient.RegisterWebhookAsync(
                accessToken,
                connection.CloudId!,
                new JiraRegisterWebhookRequest
                {
                    Name = $"Skemex {connection.ExternalProjectKey}",
                    Url = callbackUrl,
                    ProjectKey = connection.ExternalProjectKey!,
                    Events = JiraWebhookEventCatalog.OAuthDynamicJiraEventNames,
                },
                cancellationToken);

            connection.WebhookId = registered.Id;
            connection.WebhookRegisteredAt = DateTimeOffset.UtcNow;
            connection.LastError = null;
            await connectionRepository.UpdateAsync(connection, cancellationToken);
            logger.LogInformation(
                "Registered Jira dynamic webhook {WebhookId} for project {ProjectKey} ({EventCount} OAuth-supported events)",
                registered.Id,
                connection.ExternalProjectKey,
                JiraWebhookEventCatalog.OAuthDynamicJiraEventNames.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Auto-register Jira webhook failed for {ProjectKey}. Manual webhook URL is available on the connection.",
                connection.ExternalProjectKey);
            var hint = ex.Message.Contains("scope does not match", StringComparison.OrdinalIgnoreCase)
                ? "Disconnect and reconnect Jira to grant the manage:jira-webhook scope, then map the project again."
                : "Use the callback URL from integrations settings to add a webhook manually in Jira, or reconnect after updating Atlassian app scopes.";
            connection.LastError = $"Could not auto-register the Jira webhook. {hint}";
            await connectionRepository.UpdateAsync(connection, cancellationToken);
        }
    }

    public async Task TryUnregisterAsync(
        IntegrationConnection connection,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connection.WebhookId)
            || string.IsNullOrWhiteSpace(connection.CloudId)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        try
        {
            await jiraApiClient.DeleteWebhookAsync(
                accessToken,
                connection.CloudId,
                connection.WebhookId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to delete Jira webhook {WebhookId} on disconnect",
                connection.WebhookId);
        }
    }

    public string? BuildCallbackUrl(IntegrationConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.WebhookSecret))
        {
            return null;
        }

        var baseUrl = ResolvePublicApiBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        // Single shared path for the Jira integration; connection is identified by token.
        return $"{baseUrl}/api/integrations/jira/webhook"
               + $"?token={Uri.EscapeDataString(connection.WebhookSecret)}";
    }

    public IReadOnlyList<JiraWebhookEventSettingDto> GetEventSettings(IntegrationConnection connection)
    {
        var settings = JiraWebhookEventSettingsHelper.Read(connection);
        return JiraWebhookEventCatalog.All
            .Select(def => new JiraWebhookEventSettingDto
            {
                Key = def.Key,
                DisplayName = def.DisplayName,
                JiraEventName = def.JiraEventName,
                Enabled = settings.TryGetValue(def.Key, out var enabled) && enabled,
            })
            .ToList();
    }

    public void ApplyEventSettings(
        IntegrationConnection connection,
        IReadOnlyDictionary<string, bool> enabledByKey)
    {
        JiraWebhookEventSettingsHelper.Write(connection, enabledByKey);
    }

    public async Task<ErrorOr<Success>> HandleInboundAsync(
        string? token,
        string rawJsonBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Error.Unauthorized("Jira.WebhookUnauthorized", "Missing webhook token.");
        }

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.Provider == IntegrationProvider.Jira
                && c.WebhookSecret == token
                && c.Status == IntegrationConnectionStatus.Connected,
            cancellationToken: cancellationToken);

        if (connection is null || string.IsNullOrWhiteSpace(connection.WebhookSecret))
        {
            return Error.Unauthorized("Jira.WebhookUnauthorized", "Invalid webhook token.");
        }

        if (!SecureTokenEquals(connection.WebhookSecret, token))
        {
            return Error.Unauthorized("Jira.WebhookUnauthorized", "Invalid webhook token.");
        }

        if (string.IsNullOrWhiteSpace(rawJsonBody))
        {
            return Result.Success;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawJsonBody);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Jira webhook JSON");
            return Error.Validation("Jira.WebhookInvalid", "Webhook payload was not valid JSON.");
        }

        using var _ = document;
        var root = document.RootElement;
        var jiraEvent = root.TryGetProperty("webhookEvent", out var eventEl)
            ? eventEl.GetString()
            : null;
        var definition = JiraWebhookEventCatalog.FindByJiraEvent(jiraEvent);
        if (definition is null)
        {
            logger.LogDebug("Ignoring unsupported Jira webhookEvent {Event}", jiraEvent);
            return Result.Success;
        }

        if (!JiraWebhookEventSettingsHelper.IsEnabled(connection, definition.Key))
        {
            logger.LogDebug(
                "Skipping disabled webhook event {EventKey} for connection {ConnectionId}",
                definition.Key,
                connection.Id);
            return Result.Success;
        }

        if (!_handlers.TryGetValue(definition.Key, out var handler))
        {
            logger.LogWarning("No handler registered for webhook event {EventKey}", definition.Key);
            return Result.Success;
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            logger.LogWarning(
                "Webhook {EventKey} skipped: could not refresh Jira token for connection {ConnectionId}",
                definition.Key,
                connection.Id);
            return Result.Success;
        }

        var context = new JiraWebhookEventContext
        {
            Connection = connection,
            EventKey = definition.Key,
            JiraEventName = definition.JiraEventName,
            Payload = root,
            AccessToken = tokenResult.Value,
        };

        var ambientUserId = connection.ConnectedByUserId ?? Guid.Empty;
        using var ambient = AmbientUserContext.Use(connection.TenantId, ambientUserId);

        try
        {
            return await handler.HandleAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Webhook handler failed for {EventKey} on connection {ConnectionId}",
                definition.Key,
                connection.Id);
            // Ack to Jira to avoid endless retries on poison payloads.
            return Result.Success;
        }
    }

    private string? ResolvePublicApiBaseUrl()
    {
        var configured = jiraOptions.Value.PublicApiBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var redirect = jiraOptions.Value.RedirectUri?.Trim();
        if (!string.IsNullOrWhiteSpace(redirect)
            && Uri.TryCreate(redirect, UriKind.Absolute, out var redirectUri))
        {
            return $"{redirectUri.Scheme}://{redirectUri.Authority}";
        }

        var callbackBase = externalAuthOptions.Value.CallbackBaseUrl?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(callbackBase) ? null : callbackBase;
    }

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool SecureTokenEquals(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
