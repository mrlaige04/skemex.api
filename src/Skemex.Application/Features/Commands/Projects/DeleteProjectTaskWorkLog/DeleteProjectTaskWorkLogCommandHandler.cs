using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectTaskWorkLog;

public sealed class DeleteProjectTaskWorkLogCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository)
    : ICommandHandler<DeleteProjectTaskWorkLogCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectTaskWorkLogCommand request,
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

        var entry = await workLogRepository.GetAsync(
            filter: log =>
                log.Id == request.WorkLogId
                && log.TaskId == request.TaskId
                && log.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (entry is null)
        {
            return Error.NotFound("WorkLog.NotFound", "Work log was not found.");
        }

        var task = await projectTaskRepository.GetAsync(
            filter: t => t.Id == request.TaskId && t.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var spent = entry.SpentMinutes;
        await workLogRepository.DeleteAsync(entry, cancellationToken);

        task.SpentMinutes = Math.Max(0, task.SpentMinutes - spent);
        ProjectTaskTimeTracking.RecalculateRemaining(task);
        task.UpdatedAt = DateTime.UtcNow;
        await projectTaskRepository.UpdateAsync(task, cancellationToken);

        return Result.Success;
    }
}
