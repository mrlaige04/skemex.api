using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.TenantColumns.GetTenantColumnById;

public sealed class GetTenantColumnByIdQuery : IQuery<TenantColumnDto>
{
    public Guid ColumnId { get; init; }
}
