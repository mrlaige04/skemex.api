using System.Text;
using System.Text.RegularExpressions;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Services.Documents;

public sealed partial class TextChunker : ITextChunker
{
    public IReadOnlyList<string> Chunk(
        string text,
        int minChunkSize = 500,
        int maxChunkSize = 1000,
        int overlap = 125)
    {
        var normalized = NormalizeWhitespace(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        minChunkSize = Math.Clamp(minChunkSize, 100, maxChunkSize);
        maxChunkSize = Math.Max(minChunkSize, maxChunkSize);
        overlap = Math.Clamp(overlap, 0, maxChunkSize - 1);

        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var end = Math.Min(start + maxChunkSize, normalized.Length);
            if (end < normalized.Length)
            {
                end = FindBreakPoint(normalized, start, end, minChunkSize);
            }

            var chunk = normalized[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(start + 1, end - overlap);
        }

        return chunks;
    }

    private static string NormalizeWhitespace(string text)
    {
        var collapsed = WhitespaceRegex().Replace(text.Trim(), " ");
        return collapsed;
    }

    private static int FindBreakPoint(string text, int start, int end, int minChunkSize)
    {
        var minEnd = Math.Min(text.Length, start + minChunkSize);
        for (var index = end - 1; index >= minEnd; index--)
        {
            if (text[index] is '.' or '!' or '?' or '\n')
            {
                return index + 1;
            }
        }

        for (var index = end - 1; index >= minEnd; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index;
            }
        }

        return end;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
