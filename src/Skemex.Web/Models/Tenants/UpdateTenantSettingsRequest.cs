namespace Skemex.Web.Models.Tenants;

public sealed class UpdateTenantSettingsRequest
{
    public Guid? DefaultAiModelId { get; set; }
    public bool ClearDefaultAiModel { get; set; }
    public Guid? EnrichmentAiModelId { get; set; }
    public bool ClearEnrichmentAiModel { get; set; }
    public double? AiTemperature { get; set; }
}
