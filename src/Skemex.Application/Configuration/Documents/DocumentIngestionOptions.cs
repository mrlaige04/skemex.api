namespace Skemex.Application.Configuration.Documents;

public sealed class DocumentIngestionOptions
{
    public const string SectionName = "DocumentIngestion";

    /// <summary>Chunks processed per enrichment LLM request (clamped to 5–8).</summary>
    public int EnrichmentBatchSize { get; set; } = 6;
}
