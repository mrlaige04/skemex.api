using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai;

public sealed class AiProviderAuthConfig
{
    [JsonPropertyName("entries")]
    public List<AiProviderAuthEntry> Entries { get; set; } = [];
}

public sealed class AiProviderAuthEntry
{
    /// <summary><c>header</c> or <c>query</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "header";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Plaintext over the wire; ciphertext when persisted.</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public static class AiProviderAuthEntryTypes
{
    public const string Header = "header";
    public const string Query = "query";
}
