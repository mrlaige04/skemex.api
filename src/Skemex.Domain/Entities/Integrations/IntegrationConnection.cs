using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Integrations;

/// <summary>OAuth connection between a Skemex project and an external system (e.g. Jira Cloud).</summary>
public class IntegrationConnection : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public IntegrationProvider Provider { get; set; }

    public IntegrationConnectionStatus Status { get; set; } =
        IntegrationConnectionStatus.PendingProjectMapping;

    /// <summary>Atlassian cloud ID for the selected site.</summary>
    public string? CloudId { get; set; }

    public string? SiteUrl { get; set; }

    public string? SiteName { get; set; }

    public string? ExternalProjectId { get; set; }

    public string? ExternalProjectKey { get; set; }

    public string? ExternalProjectName { get; set; }

    /// <summary>AES-GCM protected access token (via <c>IEncryptService</c>).</summary>
    public string? EncryptedAccessToken { get; set; }

    /// <summary>AES-GCM protected refresh token.</summary>
    public string? EncryptedRefreshToken { get; set; }

    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    public string? Scopes { get; set; }

    public string? LastError { get; set; }

    public Guid? ConnectedByUserId { get; set; }

    /// <summary>Jira webhook id (<c>/rest/webhooks/1.0/webhook/{id}</c>) when registered.</summary>
    public string? WebhookId { get; set; }

    /// <summary>Shared secret appended as <c>?token=</c> on the inbound webhook URL.</summary>
    public string? WebhookSecret { get; set; }

    public DateTimeOffset? WebhookRegisteredAt { get; set; }

    /// <summary>
    /// JSON map of Skemex webhook event keys → enabled.
    /// Jira still delivers all subscribed events; disabled keys are ignored locally.
    /// </summary>
    public string? WebhookEventSettingsJson { get; set; }

    public IList<IntegrationMapping> Mappings { get; set; } = [];
}
