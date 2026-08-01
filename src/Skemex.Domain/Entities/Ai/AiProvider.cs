using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Ai;

/// <summary>SuperAdmin-managed OpenAI-compatible AI provider. Global, not tenant-scoped.</summary>
public class AiProvider : BaseEntity
{
    /// <summary>Display name shown in the UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Immutable unique slug; stored on <see cref="AiModel.Provider"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>OpenAI-compatible API base URL (e.g. https://api.openai.com/v1).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key ciphertext (Data Protection). May be empty if auth is only via AuthConfig.</summary>
    public string? ApiKeyEncrypted { get; set; }

    /// <summary>
    /// JSON auth entries (header/query). Secret values are Data Protection ciphertexts.
    /// </summary>
    public string AuthConfigJson { get; set; } = "{\"entries\":[]}";

    public bool IsEnabled { get; set; } = true;
}
