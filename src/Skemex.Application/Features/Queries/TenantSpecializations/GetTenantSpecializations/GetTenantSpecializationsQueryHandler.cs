using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;

public sealed class GetTenantSpecializationsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository)
    : IQueryHandler<GetTenantSpecializationsQuery, IReadOnlyList<TenantSpecializationDto>>
{
    public async Task<ErrorOr<IReadOnlyList<TenantSpecializationDto>>> Handle(
        GetTenantSpecializationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing specializations.");
        }

        var specializations = await specializationRepository.GetAllAsync(
            include: query => query.OrderBy(entry => entry.Title),
            cancellationToken: cancellationToken);

        return specializations.Select(MapToDto).ToList();
    }

    internal static TenantSpecializationDto MapToDto(TenantSpecialization specialization) => new()
    {
        Id = specialization.Id,
        Title = specialization.Title,
        Description = specialization.Description,
        DefaultSkills = TenantUserSkillMerge.NormalizeSkills(specialization.DefaultSkills),
        CreatedAt = specialization.CreatedAt,
        UpdatedAt = specialization.UpdatedAt,
    };
}
