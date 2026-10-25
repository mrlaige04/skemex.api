namespace Skemex.Application.Services.Projects;

public static class TenantColumnDefaults
{
    public static readonly IReadOnlyList<(string Key, string Title)> KanbanColumns =
    [
        ("backlog", "Backlog"),
        ("todo", "To Do"),
        ("in-progress", "In Progress"),
        ("in-qa", "In QA"),
        ("done", "Done"),
    ];
}
