using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectTaskWorkLog;

public sealed class UpdateProjectTaskWorkLogCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    IUrlService urlService)
    : ICommandHandler<UpdateProjectTaskWorkLogCommand, ProjectTaskWorkLogDto>
{
    public async Task<ErrorOr<ProjectTaskWorkLogDto>> Handle(
        UpdateProjectTaskWorkLogCommand request,
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
            include: query => query.Include(log => log.User),
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

        var previousSpent = entry.SpentMinutes;
        var changed = false;

        var nextStarted = request.StartedAt is null ? entry.StartedAt : EnsureUtc(request.StartedAt.Value);
        var nextEnded = request.EndedAt is null ? entry.EndedAt : EnsureUtc(request.EndedAt.Value);

        if (nextStarted != entry.StartedAt || nextEnded != entry.EndedAt)
        {
            var spentResult = ProjectTaskTimeTracking.CalculateSpentMinutes(nextStarted, nextEnded);
            if (spentResult.IsError)
            {
                return spentResult.Errors;
            }

            var existingLogs = await workLogRepository.GetAllAsync(
                filter: log => log.TaskId == request.TaskId && log.ProjectId == request.ProjectId,
                cancellationToken: cancellationToken);
            var overlapResult = ProjectTaskTimeTracking.EnsureNoSignificantOverlap(
                nextStarted,
                nextEnded,
                existingLogs,
                excludeWorkLogId: entry.Id);
            if (overlapResult.IsError)
            {
                return overlapResult.Errors;
            }

            entry.StartedAt = nextStarted;
            entry.EndedAt = nextEnded;
            entry.SpentMinutes = spentResult.Value;
            changed = true;
        }

        if (request.ClearComment)
        {
            if (entry.Comment is not null)
            {
                entry.Comment = null;
                changed = true;
            }
        }
        else if (request.Comment is not null)
        {
            var trimmed = request.Comment.Trim();
            if (trimmed.Length > ProjectTaskTimeTracking.MaxCommentLength)
            {
                return Error.Validation(
                    "WorkLog.CommentTooLong",
                    $"Comment cannot exceed {ProjectTaskTimeTracking.MaxCommentLength} characters.");
            }

            var next = trimmed.Length == 0 ? null : trimmed;
            if (entry.Comment != next)
            {
                entry.Comment = next;
                changed = true;
            }
        }

        if (!changed)
        {
            var avatarsUnchanged = await ProjectTaskWorkLogDtoMapper
                .LoadAvatarUrlsAsync([entry], urlService, cancellationToken)
                .ConfigureAwait(false);
            return ProjectTaskWorkLogDtoMapper.Map(entry, avatarsUnchanged);
        }

        var spentDelta = entry.SpentMinutes - previousSpent;
        task.SpentMinutes = Math.Max(0, task.SpentMinutes + spentDelta);
        ProjectTaskTimeTracking.RecalculateRemaining(task);

        entry.UpdatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await workLogRepository.UpdateAsync(entry, cancellationToken);
        await projectTaskRepository.UpdateAsync(task, cancellationToken);

        var reloaded = await workLogRepository.GetAsync(
            filter: log => log.Id == entry.Id,
            include: query => query.Include(log => log.User),
            cancellationToken: cancellationToken);

        if (reloaded is null)
        {
            return Error.Unexpected("WorkLog.UpdateFailed", "Work log was updated but could not be loaded.");
        }

        var avatars = await ProjectTaskWorkLogDtoMapper
            .LoadAvatarUrlsAsync([reloaded], urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskWorkLogDtoMapper.Map(reloaded, avatars);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
}
