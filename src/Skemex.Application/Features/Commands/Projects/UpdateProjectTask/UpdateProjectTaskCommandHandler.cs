using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTask;

public sealed class UpdateProjectTaskCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IBaseRepository<User> userRepository,
    IUrlService urlService)
    : ICommandHandler<UpdateProjectTaskCommand, ProjectTaskDto>
{
    private const int MaxTitleLength = 256;
    private const int MaxDescriptionLength = 2000;

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
                .Include(entry => entry.Reporter)
                .Include(entry => entry.Column),
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var changed = false;
        var columnMoved = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length == 0)
            {
                return Error.Validation("ProjectTask.InvalidTitle", "Title cannot be empty.");
            }

            if (title.Length > MaxTitleLength)
            {
                return Error.Validation(
                    "ProjectTask.TitleTooLong",
                    $"Title cannot exceed {MaxTitleLength} characters.");
            }

            if (title != task.Title)
            {
                task.Title = title;
                changed = true;
            }
        }

        if (request.ClearDescription)
        {
            if (task.Description is not null)
            {
                task.Description = null;
                changed = true;
            }
        }
        else if (request.Description is not null)
        {
            var description = request.Description.Trim();
            if (description.Length == 0)
            {
                if (task.Description is not null)
                {
                    task.Description = null;
                    changed = true;
                }
            }
            else
            {
                if (description.Length > MaxDescriptionLength)
                {
                    return Error.Validation(
                        "ProjectTask.DescriptionTooLong",
                        $"Description cannot exceed {MaxDescriptionLength} characters.");
                }

                if (description != task.Description)
                {
                    task.Description = description;
                    changed = true;
                }
            }
        }

        if (request.ClearAssignee)
        {
            if (task.AssigneeId is not null)
            {
                task.AssigneeId = null;
                task.Assignee = null;
                changed = true;
            }
        }
        else if (request.AssigneeId is not null)
        {
            var assigneeValidation = await ValidateAssigneeAsync(
                request.ProjectId,
                request.AssigneeId.Value,
                cancellationToken);
            if (assigneeValidation.IsError)
            {
                return assigneeValidation.Errors;
            }

            if (task.AssigneeId != request.AssigneeId)
            {
                task.AssigneeId = request.AssigneeId;
                changed = true;
            }
        }

        if (request.ColumnId is not null && task.ProjectColumnId != request.ColumnId.Value)
        {
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

            columnMoved = true;
            changed = true;
        }

        if (!changed)
        {
            return await MapTaskAsync(request.ProjectId, task, cancellationToken);
        }

        task.UpdatedAt = DateTime.UtcNow;

        if (columnMoved)
        {
            var allTasks = await projectTaskRepository.GetAllAsync(
                filter: entry => entry.ProjectId == request.ProjectId,
                include: query => query
                    .Include(entry => entry.Assignee)
                    .Include(entry => entry.Reporter)
                    .Include(entry => entry.Column),
                cancellationToken: cancellationToken);

            var descendants = CollectDescendants(allTasks, task.Id);
            var now = task.UpdatedAt;
            task.ProjectColumnId = request.ColumnId!.Value;
            foreach (var descendant in descendants)
            {
                descendant.ProjectColumnId = request.ColumnId.Value;
                descendant.UpdatedAt = now;
            }

            var tasksToUpdate = new List<ProjectTask> { task };
            tasksToUpdate.AddRange(descendants);
            await projectTaskRepository.UpdateRangeAsync(tasksToUpdate, cancellationToken);
        }
        else
        {
            await projectTaskRepository.UpdateAsync(task, cancellationToken);
        }

        var reloaded = await projectTaskRepository.GetAsync(
            filter: entry => entry.Id == task.Id && entry.ProjectId == request.ProjectId,
            include: query => query
                .Include(entry => entry.Assignee)
                .Include(entry => entry.Reporter)
                .Include(entry => entry.Column),
            cancellationToken: cancellationToken);

        if (reloaded is null)
        {
            return Error.Unexpected("ProjectTask.UpdateFailed", "Task was updated but could not be loaded.");
        }

        return await MapTaskAsync(request.ProjectId, reloaded, cancellationToken);
    }

    private async Task<ErrorOr<Success>> ValidateAssigneeAsync(
        Guid projectId,
        Guid assigneeId,
        CancellationToken cancellationToken)
    {
        var assigneeExists = await userRepository.ExistsAsync(
            filter: user => user.Id == assigneeId,
            cancellationToken: cancellationToken);
        if (!assigneeExists)
        {
            return Error.NotFound("User.NotFound", "Assignee was not found.");
        }

        var isProjectMember = await projectUserRepository.ExistsAsync(
            filter: membership => membership.ProjectId == projectId && membership.UserId == assigneeId,
            cancellationToken: cancellationToken);
        if (!isProjectMember)
        {
            return Error.Validation(
                "ProjectTask.AssigneeNotInProject",
                "Assignee must be a member of this project.");
        }

        return Result.Success;
    }

    private async Task<ProjectTaskDto> MapTaskAsync(
        Guid projectId,
        ProjectTask task,
        CancellationToken cancellationToken)
    {
        var allTasks = await projectTaskRepository.GetAllAsync(
            filter: entry => entry.ProjectId == projectId,
            include: query => query
                .Include(entry => entry.Assignee)
                .Include(entry => entry.Reporter)
                .Include(entry => entry.Column),
            cancellationToken: cancellationToken);

        var childrenByParentId = allTasks
            .Where(entry => entry.ParentId is not null)
            .GroupBy(entry => entry.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var avatarUrls = await ProjectTaskDtoMapper
            .LoadAvatarUrlsAsync(allTasks.Concat([task]), urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskDtoMapper.MapWithSubtasksFromLookup(task, childrenByParentId, avatarUrls);
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
