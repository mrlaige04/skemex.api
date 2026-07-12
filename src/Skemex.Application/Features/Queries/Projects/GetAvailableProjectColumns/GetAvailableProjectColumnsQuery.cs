using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.GetAvailableProjectColumns;

public sealed class GetAvailableProjectColumnsQuery : IQuery<IReadOnlyList<TenantColumnDto>>
{
    public Guid ProjectId { get; init; }
}
