using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Common;
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

        var filter = ProjectTaskQueryHelper.BuildFilter(
            request.ProjectId,
            request.ColumnId,
            request.AssigneeId,
            request.Unassigned,
            search);

        var paginated = await projectTaskRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: query => ProjectTaskQueryHelper.ApplySort(
                ProjectTaskQueryHelper.IncludeListRelations(query),
                request.Sort),
            cancellationToken: cancellationToken);

        var avatarUrls = await ProjectTaskDtoHelper
            .LoadAvatarUrlsAsync(paginated.Items, urlService, cancellationToken)
            .ConfigureAwait(false);

        var items = paginated.Items
            .Select(task => ProjectTaskDtoHelper.Map(task, avatarUrls))
            .ToList();

        return new PaginatedList<ProjectTaskDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }
}
