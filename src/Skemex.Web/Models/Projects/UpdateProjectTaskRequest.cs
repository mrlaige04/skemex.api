namespace Skemex.Web.Models.Projects;

public sealed class UpdateProjectTaskRequest
{
    public Guid? ColumnId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool ClearDescription { get; set; }
    public Guid? AssigneeId { get; set; }
    public bool ClearAssignee { get; set; }
    public Guid? ReporterId { get; set; }
    public int? OriginalEstimateMinutes { get; set; }
    public bool ClearOriginalEstimate { get; set; }
    public int? RemainingEstimateMinutes { get; set; }
    public bool ClearRemainingEstimate { get; set; }
    public decimal? StoryPoints { get; set; }
    public bool ClearStoryPoints { get; set; }
    public string? Type { get; set; }
    public List<string>? Tags { get; set; }
}
