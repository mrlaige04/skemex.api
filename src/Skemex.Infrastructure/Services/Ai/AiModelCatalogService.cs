using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai;

/// <summary>
/// Syncs remote catalogs from all enabled <see cref="IAiProvider"/>s into <c>ai_models</c>.
/// </summary>
public sealed class AiModelCatalogService(
    IAiProviderResolver providerResolver,
    IBaseRepository<AiModel> modelRepository,
    ILogger<AiModelCatalogService> logger) : IAiModelCatalogService
{
    private static readonly TimeSpan SyncTtl = TimeSpan.FromMinutes(30);
    private static readonly object SyncGate = new();
    private static DateTimeOffset? LastSyncUtc;

    public async Task<IReadOnlyList<AiModelDto>> ListAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var shouldSync = forceRefresh;
        lock (SyncGate)
        {
            if (!shouldSync
                && (LastSyncUtc is null || DateTimeOffset.UtcNow - LastSyncUtc > SyncTtl))
            {
                shouldSync = true;
            }
        }

        if (shouldSync)
        {
            await SyncAllProvidersAsync(cancellationToken).ConfigureAwait(false);
        }

        var models = await modelRepository
            .GetAllAsync(
                filter: model => model.IsActive,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return models
            .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(AiModelDto.FromEntity)
            .ToList();
    }

    private async Task SyncAllProvidersAsync(CancellationToken cancellationToken)
    {
        var providers = providerResolver.GetAllEnabled();
        if (providers.Count == 0)
        {
            logger.LogWarning(
                "No enabled AI providers are configured under Ai:Providers; returning local catalog only.");
            MarkSynced();
            return;
        }

        foreach (var provider in providers)
        {
            try
            {
                await SyncProviderAsync(provider, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Listing models must not 500 the API when a key is missing or the vendor is down.
                logger.LogWarning(
                    ex,
                    "Failed to sync AI models from provider {Provider}. Returning local catalog.",
                    provider.Name);
            }
        }

        MarkSynced();
    }

    private static void MarkSynced()
    {
        lock (SyncGate)
        {
            LastSyncUtc = DateTimeOffset.UtcNow;
        }
    }

    private async Task SyncProviderAsync(
        IAiProvider provider,
        CancellationToken cancellationToken)
    {
        var remote = await provider.ListModelsAsync(cancellationToken).ConfigureAwait(false);

        var existing = await modelRepository
            .GetAllAsync(
                filter: model => model.Provider == provider.Name,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var byExternalId = existing.ToDictionary(
            model => model.ExternalId,
            StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var remoteModel in remote)
        {
            seen.Add(remoteModel.ExternalId);
            if (byExternalId.TryGetValue(remoteModel.ExternalId, out var model))
            {
                model.IsActive = true;
                model.DisplayName = remoteModel.DisplayName;
                model.IconKey = remoteModel.IconKey;
                model.UpdatedAt = DateTime.UtcNow;
                await modelRepository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await modelRepository
                    .AddAsync(
                        new AiModel
                        {
                            Id = Guid.NewGuid(),
                            Provider = provider.Name,
                            ExternalId = remoteModel.ExternalId,
                            DisplayName = remoteModel.DisplayName,
                            IconKey = remoteModel.IconKey,
                            IsActive = true,
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var model in existing.Where(model => !seen.Contains(model.ExternalId) && model.IsActive))
        {
            model.IsActive = false;
            model.UpdatedAt = DateTime.UtcNow;
            await modelRepository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        }
    }
}
