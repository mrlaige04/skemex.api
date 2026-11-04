using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Helpers.Integrations;

public static class JiraConnectionDtoHelper
{
    public static JiraConnectionDto Map(
        ProjectIntegration integration,
        IJiraWebhookService? webhookService = null)
    {
        var webhook = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        var callbackUrl = webhookService?.BuildCallbackUrl(integration);
        return new()
        {
            Id = integration.Id,
            ProjectId = integration.ProjectId,
            Status = integration.Status.ToString(),
            CloudId = integration.CloudId,
            SiteUrl = integration.SiteUrl,
            SiteName = integration.SiteName,
            ExternalProjectId = integration.ExternalProjectId,
            ExternalProjectKey = integration.ExternalProjectKey,
            ExternalProjectName = integration.ExternalProjectName,
            LastError = integration.LastError,
            WebhookRegistered = !string.IsNullOrWhiteSpace(webhook?.ExternalWebhookId),
            WebhookCallbackUrl = callbackUrl,
            WebhookUrlNotPubliclyReachable = IsLocalOrPrivateCallback(callbackUrl),
            WebhookRegisteredAt = webhook?.RegisteredAt,
            WebhookEvents = webhookService?.GetEventSettings(integration) ?? [],
            CreatedAt = integration.CreatedAt,
            UpdatedAt = integration.UpdatedAt,
        };
    }

    private static bool IsLocalOrPrivateCallback(string? callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl)
            || !Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
               || host is "127.0.0.1" or "::1"
               || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }
}
