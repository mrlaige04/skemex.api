namespace Skemex.Application.Models.Rag;

/// <summary>A ranked project-document chunk match from vector similarity search.</summary>
public sealed class RagChunkMatch
{
    public required Guid Id { get; init; }

    public required string Text { get; init; }

    public string? FileName { get; init; }

    public double Distance { get; init; }

    public double Similarity => 1.0 - Distance;
}
