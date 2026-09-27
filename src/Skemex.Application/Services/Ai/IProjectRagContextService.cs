using Skemex.Application.Models.Rag;

namespace Skemex.Application.Services.Ai;

/// <summary>Retrieves and formats project document chunks for RAG prompt injection.</summary>
public interface IProjectRagContextService
{
    /// <summary>
    /// Embeds <paramref name="query"/> and returns a formatted context block from the most similar
    /// project chunks, or <c>null</c> when there is nothing useful.
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Multi-query semantic search: embeds each query, retrieves top matches, merges and deduplicates
    /// by chunk id, then formats a single context block (or <c>null</c>).
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns ranked chunk matches for a single query (project-scoped, similarity-filtered, Top-K).
    /// </summary>
    Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);
}
