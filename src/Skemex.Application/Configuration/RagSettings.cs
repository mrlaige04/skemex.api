namespace Skemex.Application.Configuration;

public sealed class RagSettings
{
    public const string SectionName = "RagSettings";

    /// <summary>Maximum number of document chunks to inject into prompts.</summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Minimum cosine similarity (0.0–1.0). Chunks below this cutoff are discarded.
    /// </summary>
    public double MinSimilarity { get; set; } = 0.62;

    /// <summary>Max characters kept per retrieved chunk in the prompt.</summary>
    public int MaxChunkChars { get; set; } = 1200;

    /// <summary>
    /// Cosine distance cutoff for pgvector (<c>&lt;=&gt;</c>): <c>1 - MinSimilarity</c>.
    /// </summary>
    public double MaxDistance => Math.Clamp(1.0 - MinSimilarity, 0.0, 1.0);
}
