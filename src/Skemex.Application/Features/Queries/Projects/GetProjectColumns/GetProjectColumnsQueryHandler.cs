using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectColumns;

public sealed class GetProjectColumnsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : IQueryHandler<GetProjectColumnsQuery, IReadOnlyList<ProjectColumnDto>>
{
    public async Task<ErrorOr<IReadOnlyList<ProjectColumnDto>>> Handle(
        GetProjectColumnsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var columns = await projectColumnRepository.GetAllAsync(
            filter: column => column.ProjectId == request.ProjectId,
            include: query => query.OrderBy(column => column.SortOrder).ThenBy(column => column.Title),
            cancellationToken: cancellationToken);

        var tenantColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        var tenantColumnsByKey = TenantColumnConstraints.IndexByKey(tenantColumns);

        return columns
            .Select(column => ProjectColumnDtoMapper.Map(column, tenantColumnsByKey))
            .ToList();
    }
}
