namespace Skemex.Application.Services.Ai;

/// <summary>Retrieves and formats project document chunks for RAG prompt injection.</summary>
public interface IProjectRagContextService
{
    /// <summary>
    /// Embeds <paramref name="query"/> and returns a formatted context block from the most similar
    /// project chunks, or <c>null</c> when there is nothing useful (no project, empty query, no chunks, errors).
    /// </summary>
    Task<string?> BuildContextAsync(
        Guid projectId,
        string query,
        CancellationToken cancellationToken = default);
}
