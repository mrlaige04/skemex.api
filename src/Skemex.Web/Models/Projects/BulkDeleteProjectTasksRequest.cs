namespace Skemex.Web.Models.Projects;

public sealed class BulkDeleteProjectTasksRequest
{
    public List<Guid> TaskIds { get; set; } = [];
}
