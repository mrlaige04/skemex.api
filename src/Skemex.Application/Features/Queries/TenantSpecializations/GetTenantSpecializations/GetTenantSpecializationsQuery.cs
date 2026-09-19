using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;

public sealed class GetTenantSpecializationsQuery : IQuery<IReadOnlyList<TenantSpecializationDto>>;
