using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

public class ProjectSettings : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid DefaultTaskColumnId { get; set; }
    public ProjectColumn DefaultTaskColumn { get; set; } = null!;

    /// <summary>Max AI task-tree depth (1 = root only). Default 2.</summary>
    public int AiMaxTreeDepth { get; set; } = 2;

    /// <summary>Max nodes in an AI-generated task tree (including root). Default 16.</summary>
    public int AiMaxNodes { get; set; } = 16;
}
