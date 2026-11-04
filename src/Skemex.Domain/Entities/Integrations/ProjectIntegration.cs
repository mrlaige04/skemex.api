using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Integrations;

/// <summary>
/// Project-scoped link to an external provider (e.g. Jira).
/// Unique per (Tenant, Project, Provider) — at most one Jira integration per project.
/// Distinct from future tenant/global integrations.
/// </summary>
public class ProjectIntegration : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public IntegrationProvider Provider { get; set; }

    public IntegrationStatus Status { get; set; } = IntegrationStatus.PendingProjectMapping;

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

    public IList<IntegrationWebhook> Webhooks { get; set; } = [];

    public IList<IntegrationMapping> Mappings { get; set; } = [];
}
