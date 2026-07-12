using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.TenantColumns.GetTenantColumns;

public sealed class GetTenantColumnsQuery : IQuery<IReadOnlyList<TenantColumnDto>>;
