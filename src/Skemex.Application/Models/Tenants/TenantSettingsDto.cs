using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Models.Tenants;

public sealed class TenantSettingsDto
{
    public Guid TenantId { get; init; }
    public Guid? DefaultAiModelId { get; init; }
    public Guid? EnrichmentAiModelId { get; init; }
    public double AiTemperature { get; init; }

    public static TenantSettingsDto FromEntity(TenantSettings settings) =>
        new()
        {
            TenantId = settings.TenantId,
            DefaultAiModelId = settings.AiSettings.DefaultModelId,
            EnrichmentAiModelId = settings.AiSettings.EnrichmentModelId,
            AiTemperature = Math.Clamp(
                settings.AiSettings.Temperature,
                AiSamplingDefaults.MinTemperature,
                AiSamplingDefaults.MaxTemperature),
        };
}
