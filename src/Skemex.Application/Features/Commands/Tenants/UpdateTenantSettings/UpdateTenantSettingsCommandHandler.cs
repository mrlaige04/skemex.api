using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;
using Skemex.Application.Services.Users;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Tenants.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSettings> tenantSettingsRepository,
    IBaseRepository<AiModel> aiModelRepository,
    TenantSettingsSeeder tenantSettingsSeeder)
    : ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    public async Task<ErrorOr<TenantSettingsDto>> Handle(
        UpdateTenantSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing settings.");
        }

        await tenantSettingsSeeder.EnsureDefaultsAsync(tenantId.Value, cancellationToken);

        var settings = await tenantSettingsRepository.GetAsync(
            filter: entry => entry.TenantId == tenantId.Value,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("TenantSettings.NotFound", "Workspace settings were not found.");
        }

        if (request.ClearDefaultAiModel)
        {
            settings.AiSettings.DefaultModelId = null;
        }
        else if (request.DefaultAiModelId is { } modelId)
        {
            var modelExists = await aiModelRepository.ExistsAsync(
                filter: model => model.Id == modelId && model.IsActive,
                cancellationToken: cancellationToken);
            if (!modelExists)
            {
                return Error.NotFound("AiModel.NotFound", "AI model was not found.");
            }

            settings.AiSettings.DefaultModelId = modelId;
        }

        if (request.ClearEnrichmentAiModel)
        {
            settings.AiSettings.EnrichmentModelId = null;
        }
        else if (request.EnrichmentAiModelId is { } enrichmentModelId)
        {
            var enrichmentModelExists = await aiModelRepository.ExistsAsync(
                filter: model => model.Id == enrichmentModelId && model.IsActive,
                cancellationToken: cancellationToken);
            if (!enrichmentModelExists)
            {
                return Error.NotFound("AiModel.NotFound", "Enrichment AI model was not found.");
            }

            settings.AiSettings.EnrichmentModelId = enrichmentModelId;
        }

        if (request.AiTemperature is { } temperature)
        {
            settings.AiSettings.Temperature = Math.Clamp(
                temperature,
                AiSamplingDefaults.MinTemperature,
                AiSamplingDefaults.MaxTemperature);
        }

        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken);

        return TenantSettingsDto.FromEntity(settings);
    }
}
