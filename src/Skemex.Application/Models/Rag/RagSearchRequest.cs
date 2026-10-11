namespace Skemex.Application.Models.Rag;

/// <summary>Hybrid RAG retrieval inputs produced by Stage-1 routing.</summary>
public sealed class RagSearchRequest
{
    /// <summary>Canonical technical English query used for embedding / vector search.</summary>
    public required string RefinedQueryEn { get; init; }

    /// <summary>Technical English keywords for GIN array-overlap matching.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
}
