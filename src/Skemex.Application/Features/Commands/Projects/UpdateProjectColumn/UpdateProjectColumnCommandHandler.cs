using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectColumn;

public sealed class UpdateProjectColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<UpdateProjectColumnCommand, ProjectColumnDto>
{
    public async Task<ErrorOr<ProjectColumnDto>> Handle(
        UpdateProjectColumnCommand request,
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

        var column = await projectColumnRepository.GetAsync(
            filter: entry => entry.Id == request.ColumnId && entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (column is null)
        {
            return Error.NotFound("ProjectColumn.NotFound", "Column was not found.");
        }

        var changed = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length == 0)
            {
                return Error.Validation("ProjectColumn.InvalidTitle", "Column title cannot be empty.");
            }

            if (title != column.Title)
            {
                column.Title = title;
                changed = true;
            }
        }

        if (request.Description is not null)
        {
            var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            if (description != column.Description)
            {
                column.Description = description;
                changed = true;
            }
        }

        if (changed)
        {
            await projectColumnRepository.UpdateAsync(column, cancellationToken);
        }

        var tenantColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        return ProjectColumnDtoMapper.Map(column, TenantColumnConstraints.IndexByKey(tenantColumns));
    }
}
