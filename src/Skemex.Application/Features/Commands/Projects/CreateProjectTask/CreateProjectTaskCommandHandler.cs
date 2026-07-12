using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTask;

public sealed class CreateProjectTaskCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IBaseRepository<User> userRepository,
    IProjectTaskCodeAllocator taskCodeAllocator)
    : ICommandHandler<CreateProjectTaskCommand, ProjectTaskDto>
{
    public async Task<ErrorOr<ProjectTaskDto>> Handle(
        CreateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var reporterId = currentUser.GetUserId();
        if (reporterId is null)
        {
            return Error.Unauthorized("User.Required", "Sign in before creating tasks.");
        }

        var project = await projectRepository.GetAsync(
            filter: entry => entry.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("ProjectSettings.NotFound", "Project settings were not found.");
        }

        var column = await projectColumnRepository.GetAsync(
            filter: entry => entry.Id == settings.DefaultTaskColumnId && entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (column is null)
        {
            return Error.Validation(
                "ProjectSettings.InvalidDefaultColumn",
                "Default task column is not configured for this project.");
        }

        var columnId = column.Id;

        if (request.AssigneeId is not null)
        {
            var assigneeValidation = await ValidateAssigneeAsync(request, cancellationToken);
            if (assigneeValidation.IsError)
            {
                return assigneeValidation.Errors;
            }
        }

        if (request.ParentId is not null)
        {
            var parentValidation = await ValidateParentAsync(request, columnId, cancellationToken);
            if (parentValidation.IsError)
            {
                return parentValidation.Errors;
            }
        }

        await using var transaction = await projectTaskRepository.BeginTransactionAsync(cancellationToken);

        int taskNumber;
        try
        {
            taskNumber = await taskCodeAllocator.AllocateNextNumberAsync(
                tenantId.Value,
                request.ProjectId,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Error.Unexpected(
                "ProjectTask.CodeAllocationFailed",
                "Could not allocate a task code for this project.");
        }

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            ProjectColumnId = columnId,
            ParentId = request.ParentId,
            Code = ProjectTaskCodeFormatter.Format(project.Code, taskNumber),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AssigneeId = request.AssigneeId,
            ReporterId = reporterId.Value,
        };

        await projectTaskRepository.AddAsync(task, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var created = await projectTaskRepository.GetAsync(
            filter: entry => entry.Id == task.Id,
            include: query => query
                .Include(entry => entry.Assignee)
                .Include(entry => entry.Reporter),
            cancellationToken: cancellationToken);

        return created is null
            ? Error.Unexpected("ProjectTask.CreateFailed", "Task was created but could not be loaded.")
            : ProjectTaskDtoMapper.Map(created);
    }

    private async Task<ErrorOr<Success>> ValidateAssigneeAsync(
        CreateProjectTaskCommand request,
        CancellationToken cancellationToken)
    {
        var assigneeExists = await userRepository.ExistsAsync(
            filter: user => user.Id == request.AssigneeId,
            cancellationToken: cancellationToken);
        if (!assigneeExists)
        {
            return Error.NotFound("User.NotFound", "Assignee was not found.");
        }

        var isProjectMember = await projectUserRepository.ExistsAsync(
            filter: membership => membership.ProjectId == request.ProjectId && membership.UserId == request.AssigneeId,
            cancellationToken: cancellationToken);
        if (!isProjectMember)
        {
            return Error.Validation("ProjectTask.AssigneeNotInProject", "Assignee must be a member of this project.");
        }

        return Result.Success;
    }

    private async Task<ErrorOr<ProjectTask>> ValidateParentAsync(
        CreateProjectTaskCommand request,
        Guid columnId,
        CancellationToken cancellationToken)
    {
        var parent = await projectTaskRepository.GetAsync(
            filter: task => task.Id == request.ParentId && task.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (parent is null)
        {
            return Error.NotFound("ProjectTask.ParentNotFound", "Parent task was not found.");
        }

        if (parent.ProjectColumnId != columnId)
        {
            return Error.Validation(
                "ProjectTask.ParentColumnMismatch",
                "Subtasks must use the same column as their parent task.");
        }

        return parent;
    }
}
