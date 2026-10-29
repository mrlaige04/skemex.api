using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;

namespace Skemex.Infrastructure.Services.Projects;

public sealed class ProjectTasksExportService : IProjectTasksExportService
{
    private static readonly string[] Headers =
    [
        "Code",
        "Title",
        "Type",
        "Status",
        "Parent Task",
        "Assignee",
        "Reporter",
        "Story Points",
        "Original Estimate (min)",
        "Remaining Estimate (min)",
        "Spent (min)",
        "Tags",
        "Created At",
        "Updated At",
        "Description",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keep Cyrillic / HTML readable in downloaded JSON files (default encoder \u-escapes them).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ExportFileResult Generate(
        IReadOnlyList<ExportTaskRow> rows,
        ExportTasksFormat format,
        string projectCode)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var safeCode = string.IsNullOrWhiteSpace(projectCode) ? "project" : projectCode.Trim();

        return format switch
        {
            ExportTasksFormat.Csv => new ExportFileResult
            {
                Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildCsv(rows))).ToArray(),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"{safeCode}-tasks-{stamp}.csv",
            },
            ExportTasksFormat.Markdown => new ExportFileResult
            {
                Content = Encoding.UTF8.GetBytes(BuildMarkdown(rows)),
                ContentType = "text/markdown; charset=utf-8",
                FileName = $"{safeCode}-tasks-{stamp}.md",
            },
            ExportTasksFormat.Json => new ExportFileResult
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows, JsonOptions)),
                ContentType = "application/json; charset=utf-8",
                FileName = $"{safeCode}-tasks-{stamp}.json",
            },
            _ => new ExportFileResult
            {
                Content = BuildXlsx(rows),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"{safeCode}-tasks-{stamp}.xlsx",
            },
        };
    }

    private static string BuildCsv(IReadOnlyList<ExportTaskRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Headers.Select(CsvEscape)));
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(',', Cells(row).Select(CsvEscape)));
        }

        return sb.ToString();
    }

    private static string BuildMarkdown(IReadOnlyList<ExportTaskRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", Headers)).AppendLine(" |");
        sb.Append("| ").Append(string.Join(" | ", Headers.Select(_ => "---"))).AppendLine(" |");
        foreach (var row in rows)
        {
            sb.Append("| ")
                .Append(string.Join(" | ", Cells(row).Select(MarkdownEscape)))
                .AppendLine(" |");
        }

        return sb.ToString();
    }

    private static byte[] BuildXlsx(IReadOnlyList<ExportTaskRow> rows)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();

            sheetData.AppendChild(CreateRow(1, Headers));
            uint rowIndex = 2;
            foreach (var row in rows)
            {
                sheetData.AppendChild(CreateRow(rowIndex, Cells(row)));
                rowIndex++;
            }

            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.AppendChild(
                new Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "Tasks",
                });

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Row CreateRow(uint rowIndex, IReadOnlyList<string> values)
    {
        var row = new Row { RowIndex = rowIndex };
        for (var i = 0; i < values.Count; i++)
        {
            row.AppendChild(
                new Cell
                {
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(values[i] ?? string.Empty)),
                    CellReference = $"{GetColumnName(i)}{rowIndex}",
                });
        }

        return row;
    }

    private static string GetColumnName(int zeroBasedIndex)
    {
        var dividend = zeroBasedIndex + 1;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static IReadOnlyList<string> Cells(ExportTaskRow row) =>
    [
        row.Code,
        row.Title,
        row.Type,
        row.Status,
        row.ParentTask,
        row.Assignee,
        row.Reporter,
        row.StoryPoints?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        row.OriginalEstimateMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        row.RemainingEstimateMinutes?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        row.SpentMinutes.ToString(CultureInfo.InvariantCulture),
        row.Tags,
        row.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        row.UpdatedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
        row.Description ?? string.Empty,
    ];

    private static string CsvEscape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    private static string MarkdownEscape(string? value)
    {
        var text = (value ?? string.Empty)
            .Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ');
        return text;
    }
}
