using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTask;

public sealed class UpdateProjectTaskCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectTask> projectTaskRepository)
    : ICommandHandler<UpdateProjectTaskCommand, ProjectTaskDto>
{
    public async Task<ErrorOr<ProjectTaskDto>> Handle(
        UpdateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var task = await projectTaskRepository.GetAsync(
            filter: entry => entry.Id == request.TaskId && entry.ProjectId == request.ProjectId,
            include: query => query
                .Include(entry => entry.Assignee)
                .Include(entry => entry.Reporter),
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        if (task.ParentId is not null)
        {
            return Error.Validation(
                "ProjectTask.OnlyRootTasksMovable",
                "Only top-level tasks can be moved between columns.");
        }

        var targetColumnExists = await projectColumnRepository.ExistsAsync(
            filter: column => column.Id == request.ColumnId && column.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!targetColumnExists)
        {
            return Error.NotFound("ProjectColumn.NotFound", "Column was not found.");
        }

        var allTasks = await projectTaskRepository.GetAllAsync(
            filter: entry => entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);

        var childrenByParentId = allTasks
            .Where(entry => entry.ParentId is not null)
            .GroupBy(entry => entry.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        if (task.ProjectColumnId == request.ColumnId)
        {
            return ProjectTaskDtoMapper.MapWithSubtasksFromLookup(task, childrenByParentId);
        }

        var descendants = CollectDescendants(allTasks, task.Id);
        task.ProjectColumnId = request.ColumnId;
        foreach (var descendant in descendants)
        {
            descendant.ProjectColumnId = request.ColumnId;
        }

        var tasksToUpdate = new List<ProjectTask> { task };
        tasksToUpdate.AddRange(descendants);

        await projectTaskRepository.UpdateRangeAsync(tasksToUpdate, cancellationToken);

        return ProjectTaskDtoMapper.MapWithSubtasksFromLookup(task, childrenByParentId);
    }

    private static List<ProjectTask> CollectDescendants(
        IReadOnlyList<ProjectTask> allTasks,
        Guid rootTaskId)
    {
        var childrenByParentId = allTasks
            .Where(task => task.ParentId is not null)
            .GroupBy(task => task.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var descendants = new List<ProjectTask>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootTaskId);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenByParentId.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                descendants.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return descendants;
    }
}
