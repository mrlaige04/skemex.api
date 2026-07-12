using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Services.Projects;

public static class ProjectTaskDtoMapper
{
    public static ProjectTaskDto Map(ProjectTask task)
    {
        return new ProjectTaskDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectColumnId = task.ProjectColumnId,
            ParentId = task.ParentId,
            Code = task.Code,
            Title = task.Title,
            Description = task.Description,
            Assignee = task.Assignee is null ? null : MapUser(task.Assignee),
            Reporter = MapUser(task.Reporter),
            Subtasks = [],
        };
    }

    public static IReadOnlyList<ProjectTaskDto> MapRootsWithSubtasks(
        IReadOnlyList<ProjectTask> allTasks,
        Guid columnId)
    {
        var childrenByParentId = allTasks
            .Where(task => task.ParentId is not null)
            .GroupBy(task => task.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var roots = allTasks
            .Where(task => task.ProjectColumnId == columnId && task.ParentId is null)
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Title)
            .ThenBy(task => task.Code)
            .Select(task => MapWithSubtasksRecursive(task, childrenByParentId))
            .ToList();

        return roots;
    }

    public static ProjectTaskDto MapWithSubtasksFromLookup(
        ProjectTask task,
        IReadOnlyDictionary<Guid, List<ProjectTask>> childrenByParentId)
    {
        return MapWithSubtasksRecursive(task, childrenByParentId);
    }

    private static ProjectTaskDto MapWithSubtasksRecursive(
        ProjectTask task,
        IReadOnlyDictionary<Guid, List<ProjectTask>> childrenByParentId)
    {
        var subtasks = childrenByParentId.TryGetValue(task.Id, out var children)
            ? children
                .OrderBy(child => child.CreatedAt)
                .ThenBy(child => child.Title)
                .ThenBy(child => child.Code)
                .Select(child => MapWithSubtasksRecursive(child, childrenByParentId))
                .ToList()
            : [];

        return new ProjectTaskDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectColumnId = task.ProjectColumnId,
            ParentId = task.ParentId,
            Code = task.Code,
            Title = task.Title,
            Description = task.Description,
            Assignee = task.Assignee is null ? null : MapUser(task.Assignee),
            Reporter = MapUser(task.Reporter),
            Subtasks = subtasks,
        };
    }

    private static ProjectTaskUserDto MapUser(User user)
    {
        return new ProjectTaskUserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
        };
    }
}
