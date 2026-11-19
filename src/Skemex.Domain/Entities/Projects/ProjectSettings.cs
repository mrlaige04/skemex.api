using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Domain.Entities.Projects;

public class ProjectSettings : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid DefaultTaskColumnId { get; set; }
    public ProjectColumn DefaultTaskColumn { get; set; } = null!;

    /// <summary>Project AI configuration (owned; columns live on <c>project_settings</c>).</summary>
    public AiSettings AiSettings { get; set; } = new();
}
