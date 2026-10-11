namespace Skemex.Application.Services.Documents;

/// <summary>Per-chunk enrichment produced before embedding.</summary>
public sealed class EnrichedChunk
{
    /// <summary>Original chunk text (unchanged).</summary>
    public required string Text { get; init; }

    /// <summary>Technical English translation, or null when already English.</summary>
    public string? EnglishText { get; init; }

    /// <summary>Technical English keywords (typically 5–10).</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
}

/// <summary>Batch-translates and keyword-tags document chunks for hybrid RAG indexing.</summary>
public interface IDocumentChunkEnrichmentService
{
    /// <summary>
    /// Enriches <paramref name="chunkTexts"/> in batches using <paramref name="modelExternalId"/>.
    /// Returns one <see cref="EnrichedChunk"/> per input, preserving order and original text.
    /// </summary>
    Task<IReadOnlyList<EnrichedChunk>> EnrichAsync(
        IReadOnlyList<string> chunkTexts,
        string? modelExternalId,
        CancellationToken cancellationToken = default);
}
