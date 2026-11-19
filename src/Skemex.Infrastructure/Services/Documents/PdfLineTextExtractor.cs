using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Skemex.Infrastructure.Services.Documents;

/// <summary>
/// Reconstructs PDF text by clustering words into lines (Y), then pages into paragraphs by vertical gaps.
/// Does not invent Markdown tables from geometry — complex PDF tables remain linear text.
/// </summary>
internal static class PdfLineTextExtractor
{
    private const double LineYTolerance = 2.0;
    private const double ParagraphGapFactor = 1.6;

    public static string Extract(Stream content)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var document = PdfDocument.Open(content);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = ExtractPage(page);
            if (string.IsNullOrWhiteSpace(pageText))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append(pageText);
        }

        return builder.ToString().Trim();
    }

    private static string ExtractPage(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
        {
            // Fallback for odd PDFs with no word segmentation.
            return page.Text?.Trim() ?? string.Empty;
        }

        // Top-to-bottom: PdfPig Y grows upward.
        var lines = ClusterWordsIntoLines(words);
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var lineHeights = new List<double>();
        for (var i = 1; i < lines.Count; i++)
        {
            var gap = Math.Abs(lines[i - 1].Y - lines[i].Y);
            if (gap > 0.1)
            {
                lineHeights.Add(gap);
            }
        }

        var medianGap = lineHeights.Count == 0
            ? 12.0
            : lineHeights.OrderBy(g => g).ElementAt(lineHeights.Count / 2);
        var paragraphGap = Math.Max(medianGap * ParagraphGapFactor, medianGap + 2.0);

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                var gap = Math.Abs(lines[i - 1].Y - lines[i].Y);
                builder.Append(gap >= paragraphGap ? "\n\n" : "\n");
            }

            builder.Append(lines[i].Text);
        }

        return builder.ToString().Trim();
    }

    private static List<(double Y, string Text)> ClusterWordsIntoLines(IReadOnlyList<Word> words)
    {
        var ordered = words
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .ThenBy(word => word.BoundingBox.Left)
            .ToList();

        var lines = new List<(double Y, List<Word> Words)>();
        foreach (var word in ordered)
        {
            var y = word.BoundingBox.Bottom;
            var line = lines.LastOrDefault();
            if (line.Words is not null && Math.Abs(line.Y - y) <= LineYTolerance)
            {
                line.Words.Add(word);
                // Keep a stable Y for the line (average).
                var updatedY = (line.Y * (line.Words.Count - 1) + y) / line.Words.Count;
                lines[^1] = (updatedY, line.Words);
            }
            else
            {
                lines.Add((y, [word]));
            }
        }

        return lines
            .Select(line =>
            {
                var text = string.Join(
                    " ",
                    line.Words
                        .OrderBy(word => word.BoundingBox.Left)
                        .Select(word => word.Text.Trim())
                        .Where(token => token.Length > 0));
                return (line.Y, text);
            })
            .Where(line => !string.IsNullOrWhiteSpace(line.text))
            .ToList();
    }
}
