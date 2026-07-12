using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectColumn;

public sealed class CreateProjectColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<CreateProjectColumnCommand, ProjectColumnDto>
{
    public async Task<ErrorOr<ProjectColumnDto>> Handle(
        CreateProjectColumnCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
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

        string key;
        string title;
        string? description;
        int sortOrder;

        if (request.TenantColumnId is not null)
        {
            var fromTenant = await CreateFromTenantColumnAsync(request, cancellationToken);
            if (fromTenant.IsError)
            {
                return fromTenant.Errors;
            }

            (key, title, description, sortOrder) = fromTenant.Value;
        }
        else
        {
            key = CreateProjectColumnCommand.NormalizeKey(request.Key!);
            if (key.Length == 0)
            {
                return Error.Validation("ProjectColumn.InvalidKey", "Column key is invalid.");
            }

            title = request.Title!.Trim();
            description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            sortOrder = await GetNextSortOrderAsync(request.ProjectId, cancellationToken);
        }

        var keyExists = await projectColumnRepository.ExistsAsync(
            filter: column => column.ProjectId == request.ProjectId && column.Key == key,
            cancellationToken: cancellationToken);
        if (keyExists)
        {
            return Error.Conflict("ProjectColumn.KeyAlreadyExists", "This column already exists on the project.");
        }

        var sortOrderTaken = await projectColumnRepository.ExistsAsync(
            filter: column => column.ProjectId == request.ProjectId && column.SortOrder == sortOrder,
            cancellationToken: cancellationToken);
        if (sortOrderTaken)
        {
            return Error.Conflict(
                "ProjectColumn.SortOrderTaken",
                $"Position {sortOrder + 1} is already used by another column on this project.");
        }

        var column = new ProjectColumn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            ProjectId = request.ProjectId,
            Key = key,
            Title = title,
            Description = description,
            SortOrder = sortOrder,
        };

        await projectColumnRepository.AddAsync(column, cancellationToken);

        var tenantColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        return ProjectColumnDtoMapper.Map(column, TenantColumnConstraints.IndexByKey(tenantColumns));
    }

    private async Task<ErrorOr<(string Key, string Title, string? Description, int SortOrder)>>
        CreateFromTenantColumnAsync(
            CreateProjectColumnCommand request,
            CancellationToken cancellationToken)
    {
        var tenantColumn = await tenantColumnRepository.GetAsync(
            filter: column => column.Id == request.TenantColumnId,
            cancellationToken: cancellationToken);
        if (tenantColumn is null)
        {
            return Error.NotFound("TenantColumn.NotFound", "Workspace column was not found.");
        }

        var sortOrder = tenantColumn.IsSortOrderForced
            ? tenantColumn.SortOrder
            : await GetNextSortOrderAsync(request.ProjectId, cancellationToken);

        return (tenantColumn.Key, tenantColumn.Title, tenantColumn.Description, sortOrder);
    }

    private async Task<int> GetNextSortOrderAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var existingColumns = await projectColumnRepository.GetAllAsync(
            filter: column => column.ProjectId == projectId,
            cancellationToken: cancellationToken);

        return existingColumns.Count == 0
            ? 0
            : existingColumns.Max(column => column.SortOrder) + 1;
    }
}
