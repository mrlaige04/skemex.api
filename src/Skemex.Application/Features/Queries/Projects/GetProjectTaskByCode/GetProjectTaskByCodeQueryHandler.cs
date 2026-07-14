using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTaskByCode;

public sealed class GetProjectTaskByCodeQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTaskByCodeQuery, ProjectTaskDto>
{
    public async Task<ErrorOr<ProjectTaskDto>> Handle(
        GetProjectTaskByCodeQuery request,
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

        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return Error.Validation("ProjectTask.InvalidCode", "Task code is required.");
        }

        var allTasks = await projectTaskRepository.GetAllAsync(
            filter: task => task.ProjectId == request.ProjectId,
            include: query => query
                .Include(task => task.Assignee)
                .Include(task => task.Reporter)
                .Include(task => task.Column),
            cancellationToken: cancellationToken);

        var task = allTasks.FirstOrDefault(entry =>
            string.Equals(entry.Code, code, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            return Error.NotFound("ProjectTask.NotFound", "Task was not found.");
        }

        var childrenByParentId = allTasks
            .Where(entry => entry.ParentId is not null)
            .GroupBy(entry => entry.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var avatarUrls = await ProjectTaskDtoMapper
            .LoadAvatarUrlsAsync(allTasks, urlService, cancellationToken)
            .ConfigureAwait(false);

        return ProjectTaskDtoMapper.MapWithSubtasksFromLookup(task, childrenByParentId, avatarUrls);
    }
}
