namespace Skemex.Web.Models.Projects;

public sealed class UpdateProjectTaskRequest
{
    public Guid? ColumnId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool ClearDescription { get; set; }
    public Guid? AssigneeId { get; set; }
    public bool ClearAssignee { get; set; }
}
