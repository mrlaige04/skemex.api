using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

public class Project : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoBlobId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public IList<ProjectUser> Users { get; set; } = [];
    public IList<ProjectColumn> Columns { get; set; } = [];
    public IList<ProjectTask> Tasks { get; set; } = [];
    public ProjectTaskCounter? TaskCounter { get; set; }
    public ProjectSettings? Settings { get; set; }
}
