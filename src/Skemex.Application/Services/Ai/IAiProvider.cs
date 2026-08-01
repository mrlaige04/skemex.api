using Skemex.Application.Models.Ai;

namespace Skemex.Application.Services.Ai;

/// <summary>Remote model descriptor returned by a provider before persistence.</summary>
public sealed record RemoteAiModel(
    string ExternalId,
    string DisplayName,
    string? IconKey = null);

/// <summary>
/// One concrete models API (Groq, OpenRouter, …).
/// Implementations list models and complete prompts; prefer Microsoft.Extensions.AI
/// <c>IChatClient</c> internally when the vendor supports it, otherwise call the vendor REST API.
/// </summary>
public interface IAiProvider
{
    string Name { get; }

    Task<IReadOnlyList<RemoteAiModel>> ListModelsAsync(
        CancellationToken cancellationToken = default);

    Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves registered <see cref="IAiProvider"/> instances.</summary>
public interface IAiProviderResolver
{
    IAiProvider GetRequired(string providerName);

    IAiProvider GetActive();

    IReadOnlyList<IAiProvider> GetAllEnabled();
}

/// <summary>Syncs provider catalogs into the local <c>ai_models</c> table and lists them.</summary>
public interface IAiModelCatalogService
{
    Task<IReadOnlyList<AiModelDto>> ListAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
