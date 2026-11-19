using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Skemex.Infrastructure.Services.Documents;

/// <summary>
/// Walks a DOCX body in order and emits Markdown (headings, lists, tables, paragraphs).
/// </summary>
internal static partial class DocxMarkdownExtractor
{
    public static string Extract(Stream content)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var document = WordprocessingDocument.Open(content, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var numberingPart = document.MainDocumentPart?.NumberingDefinitionsPart;
        var listCounters = new Dictionary<(int NumId, int Level), int>();
        var builder = new StringBuilder();
        var previousWasList = false;

        foreach (var child in body.ChildElements)
        {
            switch (child)
            {
                case Paragraph paragraph:
                {
                    var line = FormatParagraph(paragraph, numberingPart, listCounters, out var isList);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        previousWasList = false;
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        // Keep list items contiguous; blank line between other blocks.
                        builder.Append(previousWasList && isList ? '\n' : "\n\n");
                    }

                    builder.Append(line.TrimEnd());
                    previousWasList = isList;
                    break;
                }
                case Table table:
                {
                    var tableMarkdown = FormatTable(table);
                    if (string.IsNullOrWhiteSpace(tableMarkdown))
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append("\n\n");
                    }

                    builder.Append(tableMarkdown.TrimEnd());
                    previousWasList = false;
                    break;
                }
            }
        }

        return builder.ToString().Trim();
    }

    private static string FormatParagraph(
        Paragraph paragraph,
        NumberingDefinitionsPart? numberingPart,
        IDictionary<(int NumId, int Level), int> listCounters,
        out bool isList)
    {
        isList = false;
        var text = GetParagraphText(paragraph);
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = CollapseSpaces(text);

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var headingLevel = ResolveHeadingLevel(styleId);
        if (headingLevel is > 0 and <= 6)
        {
            return new string('#', headingLevel.Value) + " " + text;
        }

        var numbering = paragraph.ParagraphProperties?.NumberingProperties;
        if (numbering?.NumberingId?.Val?.Value is int numId
            && numbering.NumberingLevelReference?.Val?.Value is int level)
        {
            isList = true;
            var indent = new string(' ', Math.Max(0, level) * 2);
            var marker = ResolveListMarker(numberingPart, numId, level, listCounters);
            return indent + marker + " " + text;
        }

        return text;
    }

    private static string FormatTable(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var grid = rows
            .Select(row => row.Elements<TableCell>()
                .Select(cell => EscapeTableCell(GetCellText(cell)))
                .ToList())
            .Where(cells => cells.Count > 0)
            .ToList();

        if (grid.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = grid.Max(row => row.Count);
        foreach (var row in grid)
        {
            while (row.Count < columnCount)
            {
                row.Add(string.Empty);
            }
        }

        var builder = new StringBuilder();
        builder.Append("| ");
        builder.Append(string.Join(" | ", grid[0]));
        builder.AppendLine(" |");

        builder.Append("| ");
        builder.Append(string.Join(" | ", Enumerable.Repeat("---", columnCount)));
        builder.AppendLine(" |");

        for (var i = 1; i < grid.Count; i++)
        {
            builder.Append("| ");
            builder.Append(string.Join(" | ", grid[i]));
            builder.AppendLine(" |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetCellText(TableCell cell)
    {
        var parts = cell.Elements<Paragraph>()
            .Select(GetParagraphText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(CollapseSpaces);

        return string.Join(" ", parts);
    }

    private static string GetParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            switch (node)
            {
                case Text text when !string.IsNullOrEmpty(text.Text):
                    builder.Append(text.Text);
                    break;
                case TabChar:
                    builder.Append(' ');
                    break;
                case Break:
                    builder.Append(' ');
                    break;
            }
        }

        return builder.ToString();
    }

    private static int? ResolveHeadingLevel(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
        {
            return null;
        }

        var match = HeadingStyleRegex().Match(styleId);
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups[1].Value);
    }

    private static string ResolveListMarker(
        NumberingDefinitionsPart? numberingPart,
        int numId,
        int level,
        IDictionary<(int NumId, int Level), int> listCounters)
    {
        var isOrdered = IsOrderedList(numberingPart, numId, level);
        if (!isOrdered)
        {
            return "-";
        }

        var key = (numId, level);
        listCounters.TryGetValue(key, out var current);
        current += 1;
        listCounters[key] = current;

        // Reset deeper levels when a shallower level continues.
        foreach (var deeper in listCounters.Keys.Where(k => k.NumId == numId && k.Level > level).ToList())
        {
            listCounters.Remove(deeper);
        }

        return current + ".";
    }

    private static bool IsOrderedList(NumberingDefinitionsPart? numberingPart, int numId, int level)
    {
        if (numberingPart?.Numbering is null)
        {
            return false;
        }

        var instance = numberingPart.Numbering
            .Elements<NumberingInstance>()
            .FirstOrDefault(n => n.NumberID?.Value == numId);
        var abstractId = instance?.AbstractNumId?.Val?.Value;
        if (abstractId is null)
        {
            return false;
        }

        var abstractNum = numberingPart.Numbering
            .Elements<AbstractNum>()
            .FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
        var levelElement = abstractNum?
            .Elements<Level>()
            .FirstOrDefault(l => l.LevelIndex?.Value == level);

        var format = levelElement?.NumberingFormat?.Val?.Value;
        if (format is null)
        {
            return false;
        }

        return format != NumberFormatValues.Bullet
            && format != NumberFormatValues.None;
    }

    private static string EscapeTableCell(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();

    private static string CollapseSpaces(string value) =>
        IntraLineWhitespaceRegex().Replace(value.Trim(), " ");

    [GeneratedRegex(@"^Heading\s*([1-6])$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingStyleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex IntraLineWhitespaceRegex();
}
