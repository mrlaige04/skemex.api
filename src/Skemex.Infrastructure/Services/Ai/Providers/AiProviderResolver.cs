using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services.Ai;

namespace Skemex.Infrastructure.Services.Ai.Providers;

public sealed class AiProviderResolver(
    IEnumerable<IAiProvider> providers,
    IOptions<AiOptions> aiOptions) : IAiProviderResolver
{
    private readonly Dictionary<string, IAiProvider> _byName = providers.ToDictionary(
        provider => provider.Name,
        StringComparer.OrdinalIgnoreCase);

    public IAiProvider GetRequired(string providerName)
    {
        if (!_byName.TryGetValue(providerName.Trim(), out var provider))
        {
            throw new InvalidOperationException(
                $"AI provider '{providerName}' is not registered. Configured: {string.Join(", ", _byName.Keys)}.");
        }

        return provider;
    }

    public IAiProvider GetActive() =>
        GetRequired(aiOptions.Value.ActiveProvider);

    public IReadOnlyList<IAiProvider> GetAllEnabled()
    {
        var options = aiOptions.Value;
        return _byName.Values
            .Where(provider =>
                options.TryGetProvider(provider.Name, out var settings)
                && settings is { Enabled: true })
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
