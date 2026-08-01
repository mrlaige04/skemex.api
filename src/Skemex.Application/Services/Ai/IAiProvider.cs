using Skemex.Application.Models.Ai;

namespace Skemex.Application.Services.Ai;

/// <summary>Remote model descriptor returned by a provider before persistence.</summary>
public sealed record RemoteAiModel(
    string ExternalId,
    string DisplayName,
    string? Author = null,
    string? IconKey = null);

/// <summary>
/// One concrete models API (Groq, OpenRouter, …).
/// Implementations list models and complete prompts; prefer Microsoft.Extensions.AI
/// <c>IChatClient</c> internally when the vendor supports it, otherwise call the vendor REST API.
/// </summary>
public interface IAiProvider
{
    /// <summary>Provider key (slug), stored on <c>ai_models.Provider</c>.</summary>
    string Name { get; }

    /// <summary>Human-readable provider name for UI grouping.</summary>
    string DisplayName { get; }

    Task<IReadOnlyList<RemoteAiModel>> ListModelsAsync(
        CancellationToken cancellationToken = default);

    Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves DB-backed <see cref="IAiProvider"/> instances.</summary>
public interface IAiProviderResolver
{
    Task<IAiProvider> GetRequiredAsync(
        string providerName,
        CancellationToken cancellationToken = default);

    Task<IAiProvider> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IAiProvider>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default);

    Task<IAiProvider?> TryGetByKeyAsync(
        string providerKey,
        bool requireEnabled = true,
        CancellationToken cancellationToken = default);
}

/// <summary>Syncs provider catalogs into the local <c>ai_models</c> table and lists them.</summary>
public interface IAiModelCatalogService
{
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
