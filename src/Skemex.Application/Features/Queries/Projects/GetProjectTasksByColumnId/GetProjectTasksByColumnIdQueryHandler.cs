using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectTasksByColumnId;

public sealed class GetProjectTasksByColumnIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectTask> projectTaskRepository)
    : IQueryHandler<GetProjectTasksByColumnIdQuery, IReadOnlyList<ProjectTaskDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectTaskDto>>> Handle(
        GetProjectTasksByColumnIdQuery request,
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

        var columnExists = await projectColumnRepository.ExistsAsync(
            filter: column => column.Id == request.ColumnId && column.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!columnExists)
        {
            return Error.NotFound("ProjectColumn.NotFound", "Column was not found.");
        }

        var tasks = await projectTaskRepository.GetAllAsync(
            filter: task => task.ProjectId == request.ProjectId,
            include: query => query
                .Include(task => task.Assignee)
                .Include(task => task.Reporter),
            cancellationToken: cancellationToken);

        return ProjectTaskDtoMapper.MapRootsWithSubtasks(tasks, request.ColumnId).ToList();
    }
}
