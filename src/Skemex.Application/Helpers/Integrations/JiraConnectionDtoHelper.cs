using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Helpers.Integrations;

public static class JiraConnectionDtoHelper
{
    public static JiraConnectionDto Map(
        IntegrationConnection connection,
        IJiraWebhookService? webhookService = null)
    {
        var callbackUrl = webhookService?.BuildCallbackUrl(connection);
        return new()
        {
            Id = connection.Id,
            ProjectId = connection.ProjectId,
            Status = connection.Status.ToString(),
            CloudId = connection.CloudId,
            SiteUrl = connection.SiteUrl,
            SiteName = connection.SiteName,
            ExternalProjectId = connection.ExternalProjectId,
            ExternalProjectKey = connection.ExternalProjectKey,
            ExternalProjectName = connection.ExternalProjectName,
            LastError = connection.LastError,
            WebhookRegistered = !string.IsNullOrWhiteSpace(connection.WebhookId),
            WebhookCallbackUrl = callbackUrl,
            WebhookUrlNotPubliclyReachable = IsLocalOrPrivateCallback(callbackUrl),
            WebhookRegisteredAt = connection.WebhookRegisteredAt,
            WebhookEvents = webhookService?.GetEventSettings(connection) ?? [],
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt,
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
