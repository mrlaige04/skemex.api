namespace Skemex.Application.Configuration;

public sealed class EmbeddingsOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>Google Gemini API key (<c>Embeddings__ApiKey</c> / GitHub secret <c>GOOGLE_GEMINI_API_KEY</c>).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-embedding-001";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public int Dimensions { get; set; } = 768;

    public int BatchSize { get; set; } = 16;

    public int MaxRetryAttempts { get; set; } = 3;
}
