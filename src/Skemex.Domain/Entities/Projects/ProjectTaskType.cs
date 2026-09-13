namespace Skemex.Domain.Entities.Projects;

public enum ProjectTaskType
{
    // Task must stay at 0: EF Core treats the CLR default as "unset" when
    // HasDefaultValue(Task) is configured. Feature=0 made Feature inserts fall
    // back to the DB default "Task".
    Task = 0,
    Feature = 1,
    Bug = 2,
}

public static class ProjectTaskTypeExtensions
{
    public static bool TryParse(string? value, out ProjectTaskType type)
    {
        type = ProjectTaskType.Task;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        // Legacy alias from earlier "Story" naming.
        if (string.Equals(trimmed, "Story", StringComparison.OrdinalIgnoreCase))
        {
            type = ProjectTaskType.Feature;
            return true;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out type)
            && Enum.IsDefined(type);
    }

    public static ProjectTaskType ParseOrDefault(string? value, ProjectTaskType fallback = ProjectTaskType.Task) =>
        TryParse(value, out var type) ? type : fallback;
}
