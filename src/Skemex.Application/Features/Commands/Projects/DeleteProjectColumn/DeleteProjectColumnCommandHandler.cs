using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Projects.DeleteProjectColumn;

public sealed class DeleteProjectColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<DeleteProjectColumnCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteProjectColumnCommand request,
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

        var tenantColumn = await tenantColumnRepository.GetAsync(
            filter: entry => entry.Key == column.Key,
            cancellationToken: cancellationToken);

        var validation = TenantColumnConstraints.ValidateDeletion(column, tenantColumn);
        if (validation.IsError)
        {
            return validation.Errors;
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: entry => entry.ProjectId == request.ProjectId,
            cancellationToken: cancellationToken);
        if (settings is not null && settings.DefaultTaskColumnId == request.ColumnId)
        {
            var replacements = await projectColumnRepository.GetAllAsync(
                filter: entry =>
                    entry.ProjectId == request.ProjectId
                    && entry.Id != request.ColumnId,
                include: query => query.OrderBy(entry => entry.SortOrder).ThenBy(entry => entry.Title),
                cancellationToken: cancellationToken);
            var replacement = replacements.FirstOrDefault();
            if (replacement is null)
            {
                return Error.Validation(
                    "ProjectSettings.CannotDeleteLastDefaultColumn",
                    "Cannot delete the only column while it is the default task column.");
            }

            settings.DefaultTaskColumnId = replacement.Id;
            await projectSettingsRepository.UpdateAsync(settings, cancellationToken);
        }

        await projectColumnRepository.DeleteAsync(column, cancellationToken);
        return Result.Success;
    }
}
