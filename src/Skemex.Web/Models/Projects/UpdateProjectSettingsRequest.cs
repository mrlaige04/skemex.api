namespace Skemex.Web.Models.Projects;

public sealed class UpdateProjectSettingsRequest
{
    public Guid? DefaultTaskColumnId { get; set; }
    public int? AiMaxTreeDepth { get; set; }
    public int? AiMaxNodes { get; set; }
}
