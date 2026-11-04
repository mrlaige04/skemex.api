using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Projects;

/// <summary>Directed relationship between two project tasks (e.g. blocked-by, relates-to, duplicates).</summary>
public class ProjectTaskRelation : TenantEntity
{
    public Guid SourceTaskId { get; set; }
    public ProjectTask SourceTask { get; set; } = null!;

    public Guid TargetTaskId { get; set; }
    public ProjectTask TargetTask { get; set; } = null!;

    public TaskRelationType RelationType { get; set; }
}
