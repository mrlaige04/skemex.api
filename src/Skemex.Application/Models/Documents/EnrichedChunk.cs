namespace Skemex.Application.Models.Documents;

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
