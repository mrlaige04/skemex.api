using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTasks;

public sealed class GetProjectTasksQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectTasksQuery, PaginatedList<ProjectTaskDto>>
{
    public async Task<ErrorOr<PaginatedList<ProjectTaskDto>>> Handle(
        GetProjectTasksQuery request,
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

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<ProjectTask, bool>> filter = BuildFilter(
            request.ProjectId,
            request.ColumnId,
            request.AssigneeId,
            request.Unassigned,
            search);

        var paginated = await projectTaskRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: query => ApplySort(
                    query
                        .Include(task => task.Assignee)
                        .Include(task => task.Reporter)
                        .Include(task => task.Column),
                    request.Sort),
            cancellationToken: cancellationToken);

        var avatarUrls = await ProjectTaskDtoMapper
            .LoadAvatarUrlsAsync(paginated.Items, urlService, cancellationToken)
            .ConfigureAwait(false);

        var items = paginated.Items
            .Select(task => ProjectTaskDtoMapper.Map(task, avatarUrls))
            .ToList();

        return new PaginatedList<ProjectTaskDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }

    private static Expression<Func<ProjectTask, bool>> BuildFilter(
        Guid projectId,
        Guid? columnId,
        Guid? assigneeId,
        bool unassigned,
        string? search)
    {
        var term = string.IsNullOrEmpty(search) ? null : search.ToLowerInvariant();

        return task =>
            task.ProjectId == projectId
            && (columnId == null || task.ProjectColumnId == columnId.Value)
            && (!unassigned || task.AssigneeId == null)
            && (unassigned || assigneeId == null || task.AssigneeId == assigneeId.Value)
            && (term == null
                || task.Code.ToLower().Contains(term)
                || task.Title.ToLower().Contains(term)
                || (task.Description != null && task.Description.ToLower().Contains(term)));
    }

    private static IQueryable<ProjectTask> ApplySort(IQueryable<ProjectTask> query, string? sort)
    {
        return (sort ?? ProjectTaskSort.CreatedAtDesc) switch
        {
            ProjectTaskSort.CreatedAtAsc => query
                .OrderBy(task => task.CreatedAt)
                .ThenBy(task => task.Code),
            ProjectTaskSort.TitleAsc => query
                .OrderBy(task => task.Title)
                .ThenBy(task => task.Code),
            ProjectTaskSort.TitleDesc => query
                .OrderByDescending(task => task.Title)
                .ThenBy(task => task.Code),
            ProjectTaskSort.CodeAsc => query.OrderBy(task => task.Code),
            ProjectTaskSort.CodeDesc => query.OrderByDescending(task => task.Code),
            _ => query
                .OrderByDescending(task => task.CreatedAt)
                .ThenBy(task => task.Code),
        };
    }
}
