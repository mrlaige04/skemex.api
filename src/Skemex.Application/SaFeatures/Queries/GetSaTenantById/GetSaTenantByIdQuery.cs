using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Queries.GetSaTenantById;

public sealed class GetSaTenantByIdQuery : IQuery<SaTenantDto>
{
    public Guid TenantId { get; init; }
}
