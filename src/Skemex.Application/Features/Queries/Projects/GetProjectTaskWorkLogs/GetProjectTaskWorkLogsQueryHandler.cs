using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskWorkLogs;

public sealed class GetProjectTaskWorkLogsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTaskWorkLogsQuery, IReadOnlyList<ProjectTaskWorkLogDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectTaskWorkLogDto>>> Handle(
        GetProjectTaskWorkLogsQuery request,
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

        var taskExists = await projectTaskRepository.ExistsAsync(
            filter: task => task.Id == request.TaskId && task.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!taskExists)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var entries = await workLogRepository.GetAllAsync(
            filter: log => log.TaskId == request.TaskId && log.ProjectId == request.ProjectId,
            include: query => query.Include(log => log.User),
            cancellationToken: cancellationToken);

        var ordered = entries
            .OrderByDescending(log => log.StartedAt)
            .ThenByDescending(log => log.CreatedAt)
            .ToList();

        var avatars = await ProjectTaskWorkLogDtoMapper
            .LoadAvatarUrlsAsync(ordered, urlService, cancellationToken)
            .ConfigureAwait(false);

        return ordered
            .Select(log => ProjectTaskWorkLogDtoMapper.Map(log, avatars))
            .ToList();
    }
}
