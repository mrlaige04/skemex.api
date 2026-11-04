using System.Text;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Helpers.Projects;

/// <summary>Token-efficient Markdown projection of recent project tasks for dependency analysis.</summary>
public static class ProjectTaskDependencyMarkdownHelper
{
    public const int DefaultWindowSize = 100;

    public static string BuildRecentTasksTable(IReadOnlyList<ProjectTask> tasks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("### RECENT_TASKS");
        builder.AppendLine("| Id | Title | Status |");
        builder.AppendLine("|---|---|---|");

        if (tasks.Count == 0)
        {
            return builder.ToString().TrimEnd();
        }

        foreach (var task in tasks)
        {
            var status = task.Column?.Title?.Trim();
            if (string.IsNullOrWhiteSpace(status))
            {
                status = task.Column?.Key?.Trim();
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Unknown";
            }

            builder.Append("| ");
            builder.Append(EscapeCell(task.Id.ToString("D")));
            builder.Append(" | ");
            builder.Append(EscapeCell(task.Title));
            builder.Append(" | ");
            builder.Append(EscapeCell(status));
            builder.AppendLine(" |");
        }

        return builder.ToString().TrimEnd();
    }

    private static string EscapeCell(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace('|', '/')
            .Trim();
    }
}
