using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;
using Skemex.Application.Models.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializationById;

public sealed class GetTenantSpecializationByIdQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository)
    : IQueryHandler<GetTenantSpecializationByIdQuery, TenantSpecializationDto>
{
    public async Task<ErrorOr<TenantSpecializationDto>> Handle(
        GetTenantSpecializationByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing specializations.");
        }

        var specialization = await specializationRepository.GetAsync(
            filter: entry => entry.Id == request.SpecializationId,
            cancellationToken: cancellationToken);

        if (specialization is null)
        {
            return Error.NotFound("TenantSpecialization.NotFound", "Specialization was not found.");
        }

        return GetTenantSpecializationsQueryHandler.MapToDto(specialization);
    }
}
