using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

public class ProjectSettings : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid DefaultTaskColumnId { get; set; }
    public ProjectColumn DefaultTaskColumn { get; set; } = null!;
}
