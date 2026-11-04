using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
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
/// Manages IntegrationWebhook rows under a Jira ProjectIntegration.
/// Event enable/disable is local-only (EnabledEventTypes flags).
/// </summary>
public sealed class JiraWebhookService(
    IOptions<JiraOptions> jiraOptions,
    IOptions<ExternalAuthOptions> externalAuthOptions,
    ITenantRepository<ProjectIntegration> integrationRepository,
    ITenantRepository<IntegrationWebhook> webhookRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient,
    IEnumerable<IJiraWebhookEventHandler> eventHandlers,
    ILogger<JiraWebhookService> logger) : IJiraWebhookService
{
    private readonly Dictionary<string, IJiraWebhookEventHandler> _handlers = eventHandlers
        .GroupBy(h => h.EventKey, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public async Task EnsureRegisteredAsync(
        ProjectIntegration integration,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        if (integration.Status != IntegrationStatus.Connected
            || string.IsNullOrWhiteSpace(integration.CloudId)
            || string.IsNullOrWhiteSpace(integration.ExternalProjectKey))
        {
            return;
        }

        await EnsureWebhooksLoadedAsync(integration, cancellationToken);
        var webhook = await GetOrCreatePrimaryWebhookAsync(integration, cancellationToken);
        JiraWebhookEventSettingsHelper.EnsureDefaults(webhook);

        var callbackUrl = BuildCallbackUrl(webhook);
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            logger.LogWarning(
                "Cannot register Jira webhook for integration {ProjectIntegrationId}: public API base URL is not configured.",
                integration.Id);
            integration.LastError =
                "Webhook URL could not be built. Set Jira:PublicApiBaseUrl or Auth:External:CallbackBaseUrl to a public HTTPS origin.";
            await integrationRepository.UpdateAsync(integration, cancellationToken);
            await webhookRepository.UpdateAsync(webhook, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(webhook.ExternalWebhookId))
        {
            try
            {
                await jiraApiClient.RefreshWebhookAsync(
                    accessToken,
                    integration.CloudId!,
                    webhook.ExternalWebhookId,
                    cancellationToken);
                webhook.RegisteredAt = DateTimeOffset.UtcNow;
                integration.LastError = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to refresh Jira webhook {WebhookId}",
                    webhook.ExternalWebhookId);
            }

            await webhookRepository.UpdateAsync(webhook, cancellationToken);
            await integrationRepository.UpdateAsync(integration, cancellationToken);
            return;
        }

        try
        {
            webhook.SubscribedEventTypes = JiraWebhookEventCatalog.OAuthDynamicFlags;
            var registered = await jiraApiClient.RegisterWebhookAsync(
                accessToken,
                integration.CloudId!,
                new JiraRegisterWebhookRequest
                {
                    Name = $"Skemex {integration.ExternalProjectKey}",
                    Url = callbackUrl,
                    ProjectKey = integration.ExternalProjectKey!,
                    Events = JiraWebhookEventCatalog.OAuthDynamicJiraEventNames,
                },
                cancellationToken);

            webhook.ExternalWebhookId = registered.Id;
            webhook.RegisteredAt = DateTimeOffset.UtcNow;
            integration.LastError = null;
            await webhookRepository.UpdateAsync(webhook, cancellationToken);
            await integrationRepository.UpdateAsync(integration, cancellationToken);
            logger.LogInformation(
                "Registered Jira dynamic webhook {WebhookId} for project {ProjectKey}",
                registered.Id,
                integration.ExternalProjectKey);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Auto-register Jira webhook failed for {ProjectKey}",
                integration.ExternalProjectKey);
            var hint = ex.Message.Contains("scope does not match", StringComparison.OrdinalIgnoreCase)
                ? "Disconnect and reconnect Jira to grant the manage:jira-webhook scope, then map the project again."
                : "Use the callback URL from integrations settings to add a webhook manually in Jira, or reconnect after updating Atlassian app scopes.";
            integration.LastError = $"Could not auto-register the Jira webhook. {hint}";
            await webhookRepository.UpdateAsync(webhook, cancellationToken);
            await integrationRepository.UpdateAsync(integration, cancellationToken);
        }
    }

    public async Task TryUnregisterAsync(
        ProjectIntegration integration,
        string? accessToken,
        CancellationToken cancellationToken = default)
    {
        await EnsureWebhooksLoadedAsync(integration, cancellationToken);
        if (string.IsNullOrWhiteSpace(integration.CloudId) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        foreach (var webhook in integration.Webhooks.ToList())
        {
            if (string.IsNullOrWhiteSpace(webhook.ExternalWebhookId))
            {
                continue;
            }

            try
            {
                await jiraApiClient.DeleteWebhookAsync(
                    accessToken,
                    integration.CloudId,
                    webhook.ExternalWebhookId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete Jira webhook {WebhookId} on disconnect",
                    webhook.ExternalWebhookId);
            }

            webhook.ExternalWebhookId = null;
            webhook.RegisteredAt = null;
            await webhookRepository.UpdateAsync(webhook, cancellationToken);
        }
    }

    public string? BuildCallbackUrl(ProjectIntegration integration)
    {
        var webhook = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        return webhook is null ? null : BuildCallbackUrl(webhook);
    }

    public IReadOnlyList<JiraWebhookEventSettingDto> GetEventSettings(ProjectIntegration integration)
    {
        var webhook = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        var enabled = webhook?.EnabledEventTypes ?? JiraWebhookEventCatalog.DefaultEnabledFlags;
        return JiraWebhookEventCatalog.All
            .Select(def => new JiraWebhookEventSettingDto
            {
                Key = def.Key,
                DisplayName = def.DisplayName,
                JiraEventName = def.JiraEventName,
                Enabled = enabled.HasFlag(def.Flag),
            })
            .ToList();
    }

    public void ApplyEventSettings(
        ProjectIntegration integration,
        IReadOnlyDictionary<string, bool> enabledByKey)
    {
        var webhook = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        if (webhook is null)
        {
            return;
        }

        JiraWebhookEventSettingsHelper.ApplyEnabledMap(webhook, enabledByKey);
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

        var webhook = await webhookRepository.GetAsync(
            filter: w => w.Secret == token,
            include: q => q.Include(w => w.ProjectIntegration),
            cancellationToken: cancellationToken);

        if (webhook?.ProjectIntegration is null
            || webhook.ProjectIntegration.Provider != IntegrationProvider.Jira
            || webhook.ProjectIntegration.Status != IntegrationStatus.Connected
            || !SecureTokenEquals(webhook.Secret, token))
        {
            return Error.Unauthorized("Jira.WebhookUnauthorized", "Invalid webhook token.");
        }

        var integration = webhook.ProjectIntegration;
        if (integration.Webhooks.Count == 0)
        {
            integration.Webhooks.Add(webhook);
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

        if (!JiraWebhookEventSettingsHelper.IsEnabled(webhook, definition.Flag))
        {
            logger.LogDebug(
                "Skipping disabled webhook event {EventKey} for integration {ProjectIntegrationId}",
                definition.Key,
                integration.Id);
            return Result.Success;
        }

        if (!_handlers.TryGetValue(definition.Key, out var handler))
        {
            logger.LogWarning("No handler registered for webhook event {EventKey}", definition.Key);
            return Result.Success;
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(integration, cancellationToken);
        if (tokenResult.IsError)
        {
            logger.LogWarning(
                "Webhook {EventKey} skipped: could not refresh Jira token for integration {ProjectIntegrationId}",
                definition.Key,
                integration.Id);
            return Result.Success;
        }

        var context = new JiraWebhookEventContext
        {
            Connection = integration,
            EventKey = definition.Key,
            JiraEventName = definition.JiraEventName,
            Payload = root,
            AccessToken = tokenResult.Value,
        };

        var ambientUserId = integration.ConnectedByUserId ?? Guid.Empty;
        using var ambient = AmbientUserContext.Use(integration.TenantId, ambientUserId);

        try
        {
            return await handler.HandleAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Webhook handler failed for {EventKey} on integration {ProjectIntegrationId}",
                definition.Key,
                integration.Id);
            return Result.Success;
        }
    }

    private async Task EnsureWebhooksLoadedAsync(ProjectIntegration integration, CancellationToken cancellationToken)
    {
        if (integration.Webhooks.Count > 0)
        {
            return;
        }

        var existing = await webhookRepository.GetAllAsync(
            filter: w => w.ProjectIntegrationId == integration.Id,
            cancellationToken: cancellationToken);
        foreach (var row in existing)
        {
            integration.Webhooks.Add(row);
        }
    }

    private async Task<IntegrationWebhook> GetOrCreatePrimaryWebhookAsync(
        ProjectIntegration integration,
        CancellationToken cancellationToken)
    {
        var existing = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        if (existing is not null)
        {
            return existing;
        }

        var created = new IntegrationWebhook
        {
            Id = Guid.NewGuid(),
            TenantId = integration.TenantId,
            ProjectIntegrationId = integration.Id,
            Secret = CreateSecret(),
            SubscribedEventTypes = JiraWebhookEventCatalog.OAuthDynamicFlags,
            EnabledEventTypes = JiraWebhookEventCatalog.DefaultEnabledFlags,
        };
        await webhookRepository.AddAsync(created, cancellationToken);
        integration.Webhooks.Add(created);
        return created;
    }

    private string? BuildCallbackUrl(IntegrationWebhook webhook)
    {
        if (string.IsNullOrWhiteSpace(webhook.Secret))
        {
            return null;
        }

        var baseUrl = ResolvePublicApiBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/api/integrations/jira/webhook"
               + $"?token={Uri.EscapeDataString(webhook.Secret)}";
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
