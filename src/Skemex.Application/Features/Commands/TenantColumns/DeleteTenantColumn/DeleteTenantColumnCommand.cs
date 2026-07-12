using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.TenantColumns.DeleteTenantColumn;

public sealed class DeleteTenantColumnCommand : ICommand
{
    public Guid ColumnId { get; init; }
}
