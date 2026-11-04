using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;

namespace Skemex.Application.Services.Integrations.Jira.Webhooks;

public static class JiraWebhookEventSettingsHelper
{
    public static IntegrationWebhook? PrimaryWebhook(ProjectIntegration integration) =>
        integration.Webhooks.OrderBy(w => w.CreatedAt).FirstOrDefault();

    public static bool IsEnabled(IntegrationWebhook? webhook, JiraWebhookEventType flag) =>
        webhook is not null && webhook.EnabledEventTypes.HasFlag(flag);

    public static bool IsEnabled(IntegrationWebhook? webhook, string eventKey)
    {
        var def = JiraWebhookEventCatalog.FindByKey(eventKey);
        return def is not null && IsEnabled(webhook, def.Flag);
    }

    public static void ApplyEnabledMap(IntegrationWebhook webhook, IReadOnlyDictionary<string, bool> enabledByKey)
    {
        webhook.EnabledEventTypes = JiraWebhookEventCatalog.FlagsFromEnabledMap(enabledByKey);
    }

    public static void EnsureDefaults(IntegrationWebhook webhook)
    {
        if (webhook.SubscribedEventTypes == JiraWebhookEventType.None)
        {
            webhook.SubscribedEventTypes = JiraWebhookEventCatalog.OAuthDynamicFlags;
        }

        if (webhook.EnabledEventTypes == JiraWebhookEventType.None)
        {
            webhook.EnabledEventTypes = JiraWebhookEventCatalog.DefaultEnabledFlags;
        }
        else
        {
            // Keep unknown future flags off; ensure known defaults for newly added flags stay unset unless present.
            // No-op merge: existing EnabledEventTypes wins.
        }
    }
}
