using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai;

/// <summary>
/// Local AI model catalog. Listing is read-only; sync writes happen only via SA commands.
/// </summary>
public sealed class AiModelCatalogService(
    IAiProviderResolver providerResolver,
    IBaseRepository<AiModel> modelRepository,
    IBaseRepository<AiProvider> providerRepository,
    ILogger<AiModelCatalogService> logger) : IAiModelCatalogService
{
    public async Task<IReadOnlyList<AiModelDto>> ListAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        // Read-only: never mutate on list. Catalog writes happen only via
        // SyncProviderByKeyAsync (SA create/update/sync commands).
        // forceRefresh is accepted for API compatibility but does not sync.
        _ = forceRefresh;

        var models = await modelRepository
            .GetAllAsync(
                filter: model => model.IsActive,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var providers = await providerRepository
            .GetAllAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var enabledKeys = providers
            .Where(provider => provider.IsEnabled)
            .Select(provider => provider.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nameByKey = providers.ToDictionary(
            provider => provider.Key,
            provider => provider.Name,
            StringComparer.OrdinalIgnoreCase);

        return models
            .Where(model => enabledKeys.Contains(model.Provider))
            .OrderBy(model =>
                nameByKey.TryGetValue(model.Provider, out var liveName) && !string.IsNullOrWhiteSpace(liveName)
                    ? liveName
                    : FirstNonEmpty(model.ProviderName, model.Provider),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(model =>
                AiModelDto.FromEntity(
                    model,
                    nameByKey.TryGetValue(model.Provider, out var liveName) ? liveName : null))
            .ToList();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    public async Task SyncProviderByKeyAsync(
        string providerKey,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return;
        }

        var key = providerKey.Trim();
        var provider = await providerResolver
            .TryGetByKeyAsync(key, requireEnabled: false, cancellationToken)
            .ConfigureAwait(false);

        if (provider is null)
        {
            logger.LogWarning("Cannot sync models: AI provider '{Provider}' was not found.", providerKey);
            return;
        }

        try
        {
            if (replaceExisting)
            {
                await DeleteProviderModelsAsync(key, cancellationToken).ConfigureAwait(false);
            }

            await SyncProviderAsync(provider, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to sync AI models from provider {Provider}.",
                provider.Name);
            throw;
        }
    }

    private async Task DeleteProviderModelsAsync(string providerKey, CancellationToken cancellationToken)
    {
        var existing = await modelRepository
            .GetAllAsync(
                filter: model => model.Provider == providerKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (existing.Count > 0)
        {
            await modelRepository.DeleteRangeAsync(existing, cancellationToken).ConfigureAwait(false);
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
                // Preserve SA-managed DisplayName and IsActive.
                var providerName = provider.DisplayName;
                var author = remoteModel.Author;
                var iconKey = remoteModel.IconKey;
                var changed =
                    !string.Equals(model.ProviderName, providerName, StringComparison.Ordinal)
                    || !string.Equals(model.Author, author, StringComparison.Ordinal)
                    || !string.Equals(model.IconKey, iconKey, StringComparison.Ordinal);

                if (!changed)
                {
                    continue;
                }

                model.ProviderName = providerName;
                model.Author = author;
                model.IconKey = iconKey;
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
                            ProviderName = provider.DisplayName,
                            ExternalId = remoteModel.ExternalId,
                            DisplayName = remoteModel.DisplayName,
                            Author = remoteModel.Author,
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
