namespace Skemex.Application.Models.Projects;

public enum ExportTasksScope
{
    AllFiltered = 0,
    Selected = 1,
}

public enum ExportTasksFormat
{
    Xlsx = 0,
    Csv = 1,
    Markdown = 2,
    Json = 3,
}

public sealed class ExportFileResult
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}

public sealed class ExportTaskRow
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ParentTask { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Assignee { get; init; } = string.Empty;
    public string Reporter { get; init; } = string.Empty;
    public decimal? StoryPoints { get; init; }
    public int? OriginalEstimateMinutes { get; init; }
    public int? RemainingEstimateMinutes { get; init; }
    public int SpentMinutes { get; init; }
    public string Tags { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
