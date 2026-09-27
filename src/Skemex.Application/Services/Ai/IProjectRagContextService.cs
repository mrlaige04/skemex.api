using Skemex.Application.Models.Rag;

namespace Skemex.Application.Services.Ai;

/// <summary>Retrieves and formats project document chunks for RAG prompt injection.</summary>
public interface IProjectRagContextService
{
    /// <summary>
    /// Hybrid retrieval (vector + keyword RRF) using <paramref name="request"/>, then formats
    /// a context block from original chunk <c>Text</c> values (or <c>null</c>).
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        RagSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience: treats <paramref name="query"/> as <see cref="RagSearchRequest.RefinedQueryEn"/>
    /// with no keywords.
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy multi-query convenience: uses the first non-empty query as refined English query.
    /// Prefer <see cref="BuildContextAsync(Guid, RagSearchRequest, CancellationToken)"/>.
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default);

    /// <summary>Hybrid retrieval returning ranked matches (original <c>Text</c> for citations).</summary>
    Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        RagSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience: treats <paramref name="query"/> as <see cref="RagSearchRequest.RefinedQueryEn"/>.
    /// </summary>
    Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy multi-query convenience. Prefer
    /// <see cref="SearchChunksAsync(Guid, RagSearchRequest, CancellationToken)"/>.
    /// </summary>
    Task<IReadOnlyList<RagChunkMatch>> SearchChunksAsync(
        Guid projectId,
        IReadOnlyList<string> queries,
        CancellationToken cancellationToken = default);
}
