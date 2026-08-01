namespace Skemex.Application.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = AiProviderNames.Ollama;
    public string DefaultModel { get; set; } = "llama3";
    public OllamaAiOptions Ollama { get; set; } = new();
}

public static class AiProviderNames
{
    public const string Ollama = "Ollama";
}

public sealed class OllamaAiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// HttpClient timeout for Ollama chat calls. Local models often need several minutes.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 600;
}
