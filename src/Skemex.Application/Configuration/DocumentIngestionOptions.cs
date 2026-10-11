namespace Skemex.Application.Configuration;

public sealed class DocumentIngestionOptions
{
    public const string SectionName = "DocumentIngestion";

    /// <summary>
    /// External model id used for chunk translation / keyword enrichment
    /// (configured in appsettings, e.g. a Gemini Flash model registered in the AI catalog).
    /// When empty, the project's default AI model is used.
    /// </summary>
    public string EnrichmentModel { get; set; } = string.Empty;

    /// <summary>Chunks processed per enrichment LLM request (clamped to 5–8).</summary>
    public int EnrichmentBatchSize { get; set; } = 6;
}
