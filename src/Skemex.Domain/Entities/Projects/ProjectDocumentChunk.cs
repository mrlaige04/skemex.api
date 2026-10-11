using Pgvector;
using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

/// <summary>Semantic chunk of a project document with hybrid retrieval fields for RAG.</summary>
public class ProjectDocumentChunk : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public ProjectDocument Document { get; set; } = null!;

    public int ChunkIndex { get; set; }

    /// <summary>Original document text, preserved untouched for citations and prompt context.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Technical English translation; null when <see cref="Text"/> is already English.</summary>
    public string? EnglishText { get; set; }

    /// <summary>Technical English keywords / domain terms for GIN overlap matching.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Single embedding vector derived from synthesized English content
    /// (<c>Keywords + EnglishText ?? Text</c>).
    /// </summary>
    public Vector Embedding { get; set; } = null!;
}
