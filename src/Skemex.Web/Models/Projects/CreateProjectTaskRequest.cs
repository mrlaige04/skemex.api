namespace Skemex.Web.Models.Projects;

public sealed class CreateProjectTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? ParentId { get; set; }
}
