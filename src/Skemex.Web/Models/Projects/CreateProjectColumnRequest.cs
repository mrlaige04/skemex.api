namespace Skemex.Web.Models.Projects;

public sealed class CreateProjectColumnRequest
{
    public Guid? TenantColumnId { get; set; }
    public string? Key { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}
