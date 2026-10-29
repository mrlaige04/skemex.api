using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Domain.Entities.Integrations;

/// <summary>Maps a local Skemex entity (task) to an external entity (Jira issue).</summary>
public class IntegrationMapping : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid IntegrationConnectionId { get; set; }
    public IntegrationConnection Connection { get; set; } = null!;

    /// <summary>Local entity kind, e.g. <c>Task</c>.</summary>
    public string LocalEntityType { get; set; } = "Task";

    public Guid LocalEntityId { get; set; }

    /// <summary>External entity kind, e.g. <c>Issue</c>.</summary>
    public string ExternalEntityType { get; set; } = "Issue";

    public string ExternalEntityId { get; set; } = string.Empty;

    public string? ExternalEntityKey { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }
}
