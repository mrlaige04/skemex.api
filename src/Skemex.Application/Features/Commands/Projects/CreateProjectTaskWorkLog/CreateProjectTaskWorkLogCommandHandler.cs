using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTaskWorkLog;

public sealed class CreateProjectTaskWorkLogCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    IUrlService urlService)
    : ICommandHandler<CreateProjectTaskWorkLogCommand, ProjectTaskWorkLogDto>
{
    public async Task<ErrorOr<ProjectTaskWorkLogDto>> Handle(
        CreateProjectTaskWorkLogCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null || userId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var startedAt = EnsureUtc(request.StartedAt);
        var endedAt = EnsureUtc(request.EndedAt);
        var spentResult = ProjectTaskTimeTracking.CalculateSpentMinutes(startedAt, endedAt);
        if (spentResult.IsError)
        {
            return spentResult.Errors;
        }

        var commentResult = TryNormalizeComment(request.Comment, out var comment);
        if (commentResult.IsError)
        {
            return commentResult.Errors;
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
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var existingLogs = await workLogRepository.GetAllAsync(
            filter: log => log.TaskId == request.TaskId && log.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        var overlapResult = ProjectTaskTimeTracking.EnsureNoSignificantOverlap(
            startedAt,
            endedAt,
            existingLogs);
        if (overlapResult.IsError)
        {
            return overlapResult.Errors;
        }

        var entry = new ProjectTaskWorkLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            UserId = userId.Value,
            StartedAt = startedAt,
            EndedAt = endedAt,
            SpentMinutes = spentResult.Value,
            Comment = comment,
        };

        await workLogRepository.AddAsync(entry, cancellationToken);

        task.SpentMinutes += spentResult.Value;
        ProjectTaskTimeTracking.RecalculateRemaining(task);
        task.UpdatedAt = DateTime.UtcNow;
        await projectTaskRepository.UpdateAsync(task, cancellationToken);

        var created = await workLogRepository.GetAsync(
            filter: log => log.Id == entry.Id,
            include: query => query.Include(log => log.User),
            cancellationToken: cancellationToken);

        if (created is null)
        {
            return Error.Unexpected("WorkLog.CreateFailed", "Work log was created but could not be loaded.");
        }

        var avatars = await ProjectTaskWorkLogDtoMapper
            .LoadAvatarUrlsAsync([created], urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskWorkLogDtoMapper.Map(created, avatars);
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    private static ErrorOr<Success> TryNormalizeComment(string? comment, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return Result.Success;
        }

        var trimmed = comment.Trim();
        if (trimmed.Length > ProjectTaskTimeTracking.MaxCommentLength)
        {
            return Error.Validation(
                "WorkLog.CommentTooLong",
                $"Comment cannot exceed {ProjectTaskTimeTracking.MaxCommentLength} characters.");
        }

        normalized = trimmed;
        return Result.Success;
    }
}
