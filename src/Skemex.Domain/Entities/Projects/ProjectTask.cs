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
    public ProjectTaskType Type { get; set; } = ProjectTaskType.Task;
    public string? Description { get; set; }

    public List<string> AcceptanceCriteria { get; set; } = [];
    public List<ProjectTaskTestCase> TestCases { get; set; } = [];
    
    public int? OriginalEstimateMinutes { get; set; }
    public int? RemainingEstimateMinutes { get; set; }
    public decimal? StoryPoints { get; set; }
    public int SpentMinutes { get; set; }

    public IList<ProjectTaskWorkLog> WorkLogs { get; set; } = [];
    public IList<ProjectTaskAttachment> Attachments { get; set; } = [];

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;

    public Guid? ParentId { get; set; }
    public ProjectTask? Parent { get; set; }
    public IList<ProjectTask> Subtasks { get; set; } = [];
}
