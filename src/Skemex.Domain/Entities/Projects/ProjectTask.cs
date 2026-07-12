using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

public class ProjectTask : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid ProjectColumnId { get; set; }
    public ProjectColumn Column { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public ProjectTask? Parent { get; set; }
    public IList<ProjectTask> Subtasks { get; set; } = [];
}
