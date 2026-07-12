using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.TenantColumns.ReorderTenantColumns;

public sealed class ReorderTenantColumnsCommand : ICommand<IReadOnlyList<TenantColumnDto>>
{
    public IReadOnlyList<Guid> ColumnIds { get; init; } = [];
}
