namespace Skemex.Application.Services.Ai.Providers;

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
