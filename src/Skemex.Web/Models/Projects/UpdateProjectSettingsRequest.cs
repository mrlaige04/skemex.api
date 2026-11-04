namespace Skemex.Web.Models.Projects;

public sealed class UpdateProjectSettingsRequest
{
    public Guid? DefaultTaskColumnId { get; set; }
    public Guid? DefaultAiModelId { get; set; }
    public bool ClearDefaultAiModel { get; set; }
}
