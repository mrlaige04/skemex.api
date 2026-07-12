using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

/// <summary>
/// A column on a project board. May be seeded from tenant defaults or defined only for this project.
/// </summary>
public class ProjectColumn : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public IList<ProjectTask> Tasks { get; set; } = [];
}
