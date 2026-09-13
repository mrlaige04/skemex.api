using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

/// <summary>A single work-log entry recording time spent on a project task.</summary>
public class ProjectTaskWorkLog : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid TaskId { get; set; }
    public ProjectTask Task { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>When the work period started (UTC).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>When the work period ended (UTC).</summary>
    public DateTime EndedAt { get; set; }

    /// <summary>Duration in whole minutes, derived from <see cref="StartedAt"/> / <see cref="EndedAt"/>.</summary>
    public int SpentMinutes { get; set; }

    public string? Comment { get; set; }
}
