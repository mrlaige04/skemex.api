using System.Text;
using System.Text.RegularExpressions;
using Skemex.Application.Services.Documents;

namespace Skemex.Infrastructure.Services.Documents;

/// <summary>
/// Structure-aware text chunker: packs whole blocks (paragraphs, Markdown tables/lists)
/// until max size, splits oversized prose by sentence then word, and applies overlap.
/// </summary>
public sealed partial class TextChunker : ITextChunker
{
    private const string ParagraphSeparator = "\n\n";

    public IReadOnlyList<string> Chunk(
        string text,
        int minChunkSize = 500,
        int maxChunkSize = 1000,
        int overlap = 125)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        minChunkSize = Math.Clamp(minChunkSize, 100, Math.Max(100, maxChunkSize));
        maxChunkSize = Math.Max(minChunkSize, maxChunkSize);
        overlap = Math.Clamp(overlap, 0, maxChunkSize - 1);

        var units = ExpandToPackableUnits(SplitIntoBlocks(text), maxChunkSize);
        if (units.Count == 0)
        {
            return [];
        }

        var chunks = new List<string>();
        var buffer = new List<string>();
        var bufferLength = 0;

        foreach (var unit in units)
        {
            var separatorLength = buffer.Count == 0 ? 0 : ParagraphSeparator.Length;
            var projected = bufferLength + separatorLength + unit.Length;

            if (buffer.Count > 0 && projected > maxChunkSize)
            {
                // Emit when we already have enough content, or the next unit cannot fit.
                if (bufferLength >= minChunkSize || unit.Length > maxChunkSize - bufferLength)
                {
                    EmitBuffer(chunks, buffer, ref bufferLength);
                    TrySeedOverlap(chunks, buffer, ref bufferLength, overlap, maxChunkSize, unit.Length);
                    separatorLength = buffer.Count == 0 ? 0 : ParagraphSeparator.Length;
                    projected = bufferLength + separatorLength + unit.Length;

                    // Overlap prefix may still leave no room — drop it and start clean.
                    if (buffer.Count > 0 && projected > maxChunkSize)
                    {
                        buffer.Clear();
                        bufferLength = 0;
                    }
                }
            }

            separatorLength = buffer.Count == 0 ? 0 : ParagraphSeparator.Length;
            buffer.Add(unit);
            bufferLength += separatorLength + unit.Length;
        }

        EmitBuffer(chunks, buffer, ref bufferLength);
        return chunks;
    }

    /// <summary>
    /// Split oversized blocks into packable pieces. Tables/lists stay row/item-aware;
    /// prose falls back to sentence then word splits.
    /// </summary>
    private static List<string> ExpandToPackableUnits(IReadOnlyList<string> blocks, int maxChunkSize)
    {
        var units = new List<string>();
        foreach (var block in blocks)
        {
            if (block.Length <= maxChunkSize)
            {
                units.Add(block);
                continue;
            }

            if (IsMarkdownTable(block) || IsMarkdownList(block))
            {
                units.AddRange(SplitOversizedStructuredBlock(block, maxChunkSize));
                continue;
            }

            units.AddRange(SplitOversizedParagraph(block, maxChunkSize));
        }

        return units;
    }

    private static void EmitBuffer(List<string> chunks, List<string> buffer, ref int bufferLength)
    {
        if (buffer.Count == 0)
        {
            return;
        }

        var chunk = string.Join(ParagraphSeparator, buffer).Trim();
        if (chunk.Length > 0)
        {
            chunks.Add(chunk);
        }

        buffer.Clear();
        bufferLength = 0;
    }

    private static void TrySeedOverlap(
        List<string> chunks,
        List<string> buffer,
        ref int bufferLength,
        int overlap,
        int maxChunkSize,
        int nextUnitLength)
    {
        if (overlap <= 0 || chunks.Count == 0)
        {
            return;
        }

        var prefix = BuildOverlapPrefix(chunks[^1], overlap);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        // Overlap is stored as its own buffer unit and joined with \n\n before the next unit.
        var projected = prefix.Length + ParagraphSeparator.Length + nextUnitLength;
        if (prefix.Length > maxChunkSize || projected > maxChunkSize)
        {
            return;
        }

        buffer.Add(prefix);
        bufferLength = prefix.Length;
    }

    /// <summary>
    /// Split into structural blocks: Markdown tables and lists stay atomic (newlines kept);
    /// other blank-line paragraphs are cleaned to single-line prose. If the document has
    /// almost no blank lines, fall back to single-newline splits for plain prose.
    /// </summary>
    private static List<string> SplitIntoBlocks(string text)
    {
        var normalized = NormalizeDocument(text);
        if (normalized.Length == 0)
        {
            return [];
        }

        var blankLineSplit = BlankLineSplitRegex()
            .Split(normalized)
            .Select(CleanBlock)
            .Where(block => block.Length > 0)
            .ToList();

        // Merge consecutive list items that were separated by blank lines into one block.
        blankLineSplit = CoalesceAdjacentListBlocks(blankLineSplit);

        if (blankLineSplit.Count <= 1 && normalized.Contains('\n') && !LooksStructured(normalized))
        {
            var lineSplit = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => IntraLineWhitespaceRegex().Replace(line.Trim(), " "))
                .Where(line => line.Length > 0)
                .ToList();

            if (lineSplit.Count > 1)
            {
                return lineSplit;
            }
        }

        return blankLineSplit;
    }

    private static string NormalizeDocument(string text)
    {
        var withUnixNewlines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var lines = withUnixNewlines.Split('\n');
        var builder = new StringBuilder(withUnixNewlines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = IntraLineWhitespaceRegex().Replace(lines[i].Trim(), " ");
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }

        return MultiBlankLineRegex().Replace(builder.ToString(), "\n\n").Trim();
    }

    private static string CleanBlock(string block)
    {
        var trimmed = block.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (IsMarkdownTable(trimmed) || IsMarkdownList(trimmed))
        {
            // Preserve internal newlines for structured Markdown.
            var lines = trimmed.Split('\n');
            var builder = new StringBuilder(trimmed.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = IntraLineWhitespaceRegex().Replace(lines[i].Trim(), " ");
                if (line.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        return IntraLineWhitespaceRegex().Replace(trimmed.Replace('\n', ' '), " ").Trim();
    }

    private static List<string> CoalesceAdjacentListBlocks(List<string> blocks)
    {
        if (blocks.Count <= 1)
        {
            return blocks;
        }

        var result = new List<string>();
        foreach (var block in blocks)
        {
            if (result.Count > 0
                && IsMarkdownList(result[^1])
                && IsMarkdownList(block)
                && !IsMarkdownTable(block))
            {
                result[^1] = result[^1] + "\n" + block;
                continue;
            }

            result.Add(block);
        }

        return result;
    }

    private static bool LooksStructured(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var structured = 0;
        foreach (var line in lines)
        {
            if (IsTableRow(line) || IsListItem(line))
            {
                structured++;
            }
        }

        return structured >= 2;
    }

    private static bool IsMarkdownTable(string block)
    {
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
        {
            return false;
        }

        var tableLines = lines.Count(IsTableRow);
        return tableLines >= 2 && tableLines >= (lines.Length + 1) / 2;
    }

    private static bool IsMarkdownList(string block)
    {
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        var listLines = lines.Count(IsListItem);
        return listLines >= 1 && listLines >= (lines.Length + 1) / 2;
    }

    private static bool IsTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|') && trimmed.Contains('|', StringComparison.Ordinal);
    }

    private static bool IsListItem(string line)
    {
        var trimmed = line.TrimStart();
        return ListItemRegex().IsMatch(trimmed);
    }

    /// <summary>
    /// Split a large table/list by whole rows/items, repeating the table header when needed.
    /// </summary>
    private static List<string> SplitOversizedStructuredBlock(string block, int maxChunkSize)
    {
        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return [];
        }

        if (IsMarkdownTable(block) && lines.Length >= 2 && IsTableSeparator(lines[1]))
        {
            return SplitOversizedTable(lines, maxChunkSize);
        }

        return PackLines(lines, maxChunkSize, separator: "\n");
    }

    private static List<string> SplitOversizedTable(string[] lines, int maxChunkSize)
    {
        var header = lines[0] + "\n" + lines[1];
        var headerLength = header.Length;
        var result = new List<string>();

        if (headerLength >= maxChunkSize)
        {
            // Pathological header — fall back to line packing without repeating header.
            return PackLines(lines, maxChunkSize, separator: "\n");
        }

        var buffer = new StringBuilder(header);
        for (var i = 2; i < lines.Length; i++)
        {
            var row = lines[i];

            // Header + single row still too large: flush current slice, then word-split.
            if (headerLength + 1 + row.Length > maxChunkSize)
            {
                if (buffer.Length > headerLength)
                {
                    result.Add(buffer.ToString());
                }

                result.AddRange(SplitByWords(header + "\n" + row, maxChunkSize));
                buffer.Clear();
                buffer.Append(header);
                continue;
            }

            var projected = buffer.Length + 1 + row.Length;
            if (projected > maxChunkSize && buffer.Length > headerLength)
            {
                result.Add(buffer.ToString());
                buffer.Clear();
                buffer.Append(header);
            }

            buffer.Append('\n');
            buffer.Append(row);
        }

        if (buffer.Length > headerLength)
        {
            result.Add(buffer.ToString());
        }
        else if (result.Count == 0)
        {
            result.Add(header);
        }

        return result;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return false;
        }

        // e.g. | --- | --- |
        return trimmed.All(ch => ch is '|' or '-' or ':' or ' ');
    }

    private static List<string> PackLines(IReadOnlyList<string> lines, int maxChunkSize, string separator)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.Length > maxChunkSize)
            {
                if (buffer.Length > 0)
                {
                    result.Add(buffer.ToString());
                    buffer.Clear();
                }

                result.AddRange(SplitByWords(line, maxChunkSize));
                continue;
            }

            var sepLen = buffer.Length == 0 ? 0 : separator.Length;
            if (buffer.Length > 0 && buffer.Length + sepLen + line.Length > maxChunkSize)
            {
                result.Add(buffer.ToString());
                buffer.Clear();
                sepLen = 0;
            }

            if (buffer.Length > 0)
            {
                buffer.Append(separator);
            }

            buffer.Append(line);
        }

        if (buffer.Length > 0)
        {
            result.Add(buffer.ToString());
        }

        return result;
    }

    private static List<string> SplitOversizedParagraph(string paragraph, int maxChunkSize)
    {
        var result = new List<string>();
        var sentences = SplitSentences(paragraph);
        if (sentences.Count == 0)
        {
            result.AddRange(SplitByWords(paragraph, maxChunkSize));
            return result;
        }

        var buffer = new StringBuilder();
        foreach (var sentence in sentences)
        {
            if (sentence.Length > maxChunkSize)
            {
                if (buffer.Length > 0)
                {
                    result.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }

                result.AddRange(SplitByWords(sentence, maxChunkSize));
                continue;
            }

            var separator = buffer.Length == 0 ? string.Empty : " ";
            if (buffer.Length > 0 && buffer.Length + separator.Length + sentence.Length > maxChunkSize)
            {
                result.Add(buffer.ToString().Trim());
                buffer.Clear();
                separator = string.Empty;
            }

            if (buffer.Length > 0)
            {
                buffer.Append(separator);
            }

            buffer.Append(sentence);
        }

        if (buffer.Length > 0)
        {
            result.Add(buffer.ToString().Trim());
        }

        return result;
    }

    private static List<string> SplitSentences(string text)
    {
        var matches = SentenceSplitRegex().Matches(text);
        if (matches.Count == 0)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text.Trim()];
        }

        return matches
            .Select(match => match.Value.Trim())
            .Where(value => value.Length > 0)
            .ToList();
    }

    private static List<string> SplitByWords(string text, int maxChunkSize)
    {
        var result = new List<string>();
        if (text.Length <= maxChunkSize)
        {
            result.Add(text.Trim());
            return result;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var buffer = new StringBuilder();
        foreach (var word in words)
        {
            if (word.Length > maxChunkSize)
            {
                if (buffer.Length > 0)
                {
                    result.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }

                for (var offset = 0; offset < word.Length; offset += maxChunkSize)
                {
                    result.Add(word.Substring(offset, Math.Min(maxChunkSize, word.Length - offset)));
                }

                continue;
            }

            var separator = buffer.Length == 0 ? string.Empty : " ";
            if (buffer.Length > 0 && buffer.Length + separator.Length + word.Length > maxChunkSize)
            {
                result.Add(buffer.ToString().Trim());
                buffer.Clear();
                separator = string.Empty;
            }

            if (buffer.Length > 0)
            {
                buffer.Append(separator);
            }

            buffer.Append(word);
        }

        if (buffer.Length > 0)
        {
            result.Add(buffer.ToString().Trim());
        }

        return result;
    }

    /// <summary>
    /// Take up to <paramref name="overlap"/> chars from the end of the previous chunk,
    /// preferring a paragraph or sentence boundary inside that window.
    /// </summary>
    private static string BuildOverlapPrefix(string previousChunk, int overlap)
    {
        if (overlap <= 0 || string.IsNullOrWhiteSpace(previousChunk))
        {
            return string.Empty;
        }

        var windowStart = Math.Max(0, previousChunk.Length - overlap);
        var window = previousChunk[windowStart..];

        var paragraphBreak = window.LastIndexOf(ParagraphSeparator, StringComparison.Ordinal);
        if (paragraphBreak >= 0 && paragraphBreak + ParagraphSeparator.Length < window.Length)
        {
            return window[(paragraphBreak + ParagraphSeparator.Length)..].Trim();
        }

        for (var index = 0; index < window.Length - 1; index++)
        {
            if (window[index] is '.' or '!' or '?' && char.IsWhiteSpace(window[index + 1]))
            {
                var candidate = window[(index + 1)..].Trim();
                if (candidate.Length > 0)
                {
                    return candidate;
                }
            }
        }

        var space = window.IndexOf(' ');
        if (space >= 0 && space + 1 < window.Length)
        {
            return window[(space + 1)..].Trim();
        }

        return window.Trim();
    }

    [GeneratedRegex(@"\n\s*\n+")]
    private static partial Regex BlankLineSplitRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex IntraLineWhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultiBlankLineRegex();

    [GeneratedRegex(@"[^.!?]+(?:[.!?]+(?:\s+|$)|$)", RegexOptions.Singleline)]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"^(?:[-*+]|\d+[.)])\s+")]
    private static partial Regex ListItemRegex();
}
