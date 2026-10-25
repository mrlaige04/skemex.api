using Skemex.Application.Models.Documents;

namespace Skemex.Application.Services.Documents;

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
