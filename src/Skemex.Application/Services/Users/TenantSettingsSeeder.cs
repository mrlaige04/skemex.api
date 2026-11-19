using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Services.Users;

/// <summary>
/// Ensures a tenant has <see cref="TenantSettings"/> with default <see cref="AiSettings"/>
/// (numeric consts + first active catalog model when available).
/// </summary>
public sealed class TenantSettingsSeeder(
    IBaseRepository<TenantSettings> tenantSettingsRepository,
    IBaseRepository<AiModel> aiModelRepository)
{
    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var exists = await tenantSettingsRepository.ExistsAsync(
            filter: settings => settings.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (exists)
        {
            return;
        }

        var activeModels = await aiModelRepository.GetAllAsync(
            filter: model => model.IsActive,
            cancellationToken: cancellationToken);

        var firstModelId = activeModels
            .OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.ExternalId, StringComparer.OrdinalIgnoreCase)
            .Select(model => (Guid?)model.Id)
            .FirstOrDefault();

        await tenantSettingsRepository.AddAsync(
            new TenantSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AiSettings = AiSettings.CreateDefault(firstModelId),
            },
            cancellationToken);
    }
}
