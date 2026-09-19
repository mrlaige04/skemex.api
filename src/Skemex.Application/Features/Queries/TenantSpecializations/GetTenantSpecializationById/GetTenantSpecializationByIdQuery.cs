using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializationById;

public sealed class GetTenantSpecializationByIdQuery : IQuery<TenantSpecializationDto>
{
    public Guid SpecializationId { get; init; }
}
