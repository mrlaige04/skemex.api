using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetAvailableProjectColumns;

public sealed class GetAvailableProjectColumnsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : IQueryHandler<GetAvailableProjectColumnsQuery, IReadOnlyList<TenantColumnDto>>
{
    public async Task<ErrorOr<IReadOnlyList<TenantColumnDto>>> Handle(
        GetAvailableProjectColumnsQuery request,
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

        var projectColumns = await projectColumnRepository.GetAllAsync(
            filter: column => column.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);

        var existingKeys = projectColumns
            .Select(column => column.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tenantColumns = await tenantColumnRepository.GetAllAsync(
            include: query => query.OrderBy(column => column.SortOrder).ThenBy(column => column.Title),
            cancellationToken: cancellationToken);

        return tenantColumns
            .Where(column => !existingKeys.Contains(column.Key))
            .Select(GetTenantColumnsQueryHandler.MapToDto)
            .ToList();
    }
}
