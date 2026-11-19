namespace Skemex.Web.Models.Projects;

public sealed class UpdateProjectSettingsRequest
{
    public Guid? DefaultTaskColumnId { get; set; }
    public Guid? DefaultAiModelId { get; set; }
    public bool ClearDefaultAiModel { get; set; }
    public Guid? EnrichmentAiModelId { get; set; }
    public bool ClearEnrichmentAiModel { get; set; }
    public double? AiTemperature { get; set; }
}
