namespace Skemex.Application.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// Fallback provider name when a model is not found in the local catalog.
    /// </summary>
    public string ActiveProvider { get; set; } = AiProviderNames.Groq;

    /// <summary>
    /// Named providers keyed by provider name (e.g. <c>Groq</c>).
    /// Bind from config: <c>Ai:Providers:Groq:ApiKey</c>.
    /// </summary>
    public Dictionary<string, AiProviderOptions> Providers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public AiProviderOptions GetRequiredProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException("AI provider name is required.");
        }

        if (!TryGetProvider(providerName, out var provider) || provider is null)
        {
            throw new InvalidOperationException(
                $"Ai:Providers:{providerName} is not configured.");
        }

        return provider;
    }

    public bool TryGetProvider(string providerName, out AiProviderOptions? provider)
    {
        provider = null;
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return false;
        }

        var match = Providers.FirstOrDefault(pair =>
            string.Equals(pair.Key, providerName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(match.Key) || match.Value is null)
        {
            return false;
        }

        provider = match.Value;
        return true;
    }

    public AiProviderOptions GetActiveProvider() => GetRequiredProvider(ActiveProvider);
}

public static class AiProviderNames
{
    public const string Groq = "Groq";
}

/// <summary>Connection settings for one AI models provider.</summary>
public sealed class AiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public bool Enabled { get; set; } = true;
}
