using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

public class ProjectTaskCounter : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int NextNumber { get; set; } = 1;
}
