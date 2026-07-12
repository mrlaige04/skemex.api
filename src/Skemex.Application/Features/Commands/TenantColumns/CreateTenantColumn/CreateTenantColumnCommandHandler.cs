using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;
using Skemex.Application.Models.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantColumns.CreateTenantColumn;

public sealed class CreateTenantColumnCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantColumn> tenantColumnRepository)
    : ICommandHandler<CreateTenantColumnCommand, TenantColumnDto>
{
    public async Task<ErrorOr<TenantColumnDto>> Handle(
        CreateTenantColumnCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing columns.");
        }

        var key = CreateTenantColumnCommand.NormalizeKey(request.Key);
        if (key.Length == 0)
        {
            return Error.Validation("TenantColumn.InvalidKey", "Column key is invalid.");
        }

        var keyExists = await tenantColumnRepository.ExistsAsync(
            filter: column => column.Key == key,
            cancellationToken: cancellationToken);
        if (keyExists)
        {
            return Error.Conflict("TenantColumn.KeyAlreadyExists", "A column with this key already exists.");
        }

        var existingColumns = await tenantColumnRepository.GetAllAsync(cancellationToken: cancellationToken);
        var sortOrder = existingColumns.Count == 0
            ? 0
            : existingColumns.Max(column => column.SortOrder) + 1;

        var column = new TenantColumn
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Key = key,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = sortOrder,
            IsRequired = request.IsRequired,
            IsSortOrderForced = request.IsSortOrderForced,
        };

        await tenantColumnRepository.AddAsync(column, cancellationToken);

        return GetTenantColumnsQueryHandler.MapToDto(column);
    }
}
