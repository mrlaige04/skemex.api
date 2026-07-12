using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaTenants;

namespace Skemex.Application.SaFeatures.Queries.SaTenants.GetSaTenantById;

public sealed class GetSaTenantByIdQuery : IQuery<SaTenantDto>
{
    public Guid TenantId { get; init; }
}
