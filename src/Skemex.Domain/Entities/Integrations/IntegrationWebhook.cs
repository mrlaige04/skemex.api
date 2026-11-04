using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Integrations;

/// <summary>
/// Provider webhook registration under a <see cref="ProjectIntegration"/>.
/// One integration may have multiple webhooks (e.g. OAuth-dynamic vs admin); typically one covers many event types.
/// </summary>
public class IntegrationWebhook : TenantEntity
{
    public Guid ProjectIntegrationId { get; set; }
    public ProjectIntegration ProjectIntegration { get; set; } = null!;

    /// <summary>External system webhook id (e.g. Jira dynamic webhook id).</summary>
    public string? ExternalWebhookId { get; set; }

    /// <summary>Shared secret used as <c>?token=</c> on the inbound callback URL.</summary>
    public string Secret { get; set; } = string.Empty;

    public DateTimeOffset? RegisteredAt { get; set; }

    /// <summary>
    /// Event types this webhook is subscribed to at the provider
    /// (for Jira: <see cref="JiraWebhookEventType"/> flags).
    /// </summary>
    public JiraWebhookEventType SubscribedEventTypes { get; set; }

    /// <summary>
    /// Event types enabled for local processing
    /// (subset of subscribed; toggling does not re-register at the provider).
    /// </summary>
    public JiraWebhookEventType EnabledEventTypes { get; set; }
}
