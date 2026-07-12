using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantColumns.UpdateTenantColumn;

public sealed class UpdateTenantColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<UpdateTenantColumnCommand, TenantColumnDto>
{
    public async Task<ErrorOr<TenantColumnDto>> Handle(
        UpdateTenantColumnCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing columns.");
        }

        var column = await tenantColumnRepository.GetAsync(
            filter: entry => entry.Id == request.ColumnId,
            cancellationToken: cancellationToken);

        if (column is null)
        {
            return Error.NotFound("TenantColumn.NotFound", "Column was not found.");
        }

        var changed = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length == 0)
            {
                return Error.Validation("TenantColumn.InvalidTitle", "Column title cannot be empty.");
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

        if (request.IsRequired is not null && request.IsRequired.Value != column.IsRequired)
        {
            column.IsRequired = request.IsRequired.Value;
            changed = true;
        }

        if (request.IsSortOrderForced is not null && request.IsSortOrderForced.Value != column.IsSortOrderForced)
        {
            column.IsSortOrderForced = request.IsSortOrderForced.Value;
            changed = true;
        }

        if (changed)
        {
            await tenantColumnRepository.UpdateAsync(column, cancellationToken);
        }

        return GetTenantColumnsQueryHandler.MapToDto(column);
    }
}
