using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

/// <summary>A rich-text comment on a project task (Jira-style discussion).</summary>
public class ProjectTaskComment : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid TaskId { get; set; }
    public ProjectTask Task { get; set; } = null!;

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    /// <summary>Comment body as HTML (same rich format as task description).</summary>
    public string Body { get; set; } = string.Empty;

    public IList<IntegrationMapping> IntegrationMappings { get; set; } = [];
}
