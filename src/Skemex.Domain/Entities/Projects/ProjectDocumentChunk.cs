using Pgvector;
using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Projects;

/// <summary>Semantic chunk of a project document with its embedding vector for RAG retrieval.</summary>
public class ProjectDocumentChunk : TenantEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public ProjectDocument Document { get; set; } = null!;

    public int ChunkIndex { get; set; }

    public string Text { get; set; } = string.Empty;

    public Vector Embedding { get; set; } = null!;
}
