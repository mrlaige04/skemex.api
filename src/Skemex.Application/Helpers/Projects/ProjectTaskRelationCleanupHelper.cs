using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Helpers.Projects;

/// <summary>
/// Removes task relations before tasks are deleted (FK is Restrict on both ends).
/// Also expands to descendants because parent deletion cascades in the DB.
/// </summary>
public static class ProjectTaskRelationCleanupHelper
{
    public static async Task<HashSet<Guid>> ExpandWithDescendantsAsync(
        ITenantRepository<ProjectTask> projectTaskRepository,
        Guid projectId,
        IEnumerable<Guid> rootTaskIds,
        CancellationToken cancellationToken)
    {
        var roots = rootTaskIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (roots.Count == 0)
        {
            return [];
        }

        var tree = await projectTaskRepository
            .GetAllWithSelectorAsync(
                task => new { task.Id, task.ParentId },
                filter: task => task.ProjectId == projectId,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var childrenByParent = tree
            .Where(task => task.ParentId is not null)
            .GroupBy(task => task.ParentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(task => task.Id).ToList());

        var expanded = new HashSet<Guid>();
        var stack = new Stack<Guid>(roots);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!expanded.Add(id))
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(id, out var children))
            {
                continue;
            }

            foreach (var childId in children)
            {
                stack.Push(childId);
            }
        }

        return expanded;
    }

    public static async Task DeleteRelationsForTasksAsync(
        ITenantRepository<ProjectTaskRelation> relationRepository,
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return;
        }

        var relations = await relationRepository
            .GetAllAsync(
                filter: relation =>
                    taskIds.Contains(relation.SourceTaskId)
                    || taskIds.Contains(relation.TargetTaskId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (relations.Count == 0)
        {
            return;
        }

        await relationRepository
            .DeleteRangeAsync(relations, cancellationToken)
            .ConfigureAwait(false);
    }
}
