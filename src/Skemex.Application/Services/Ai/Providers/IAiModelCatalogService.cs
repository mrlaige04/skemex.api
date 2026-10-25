using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Services.Ai.Providers;

/// <summary>Lists the local AI model catalog (read-only) and syncs providers via SA commands.</summary>
public interface IAiModelCatalogService
{
    /// <summary>Read-only list of active models. Does not call providers or write to the DB.</summary>
    Task<IReadOnlyList<AiModelDto>> ListAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>Sync models for a single provider key (used after SA create/update).</summary>
    /// <param name="replaceExisting">
    /// When true, deletes all local models for this provider first, then fetches fresh.
    /// </param>
    Task SyncProviderByKeyAsync(
        string providerKey,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default);
}
