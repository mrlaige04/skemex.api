using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantSpecializations.CreateTenantSpecialization;

public sealed class CreateTenantSpecializationCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository)
    : ICommandHandler<CreateTenantSpecializationCommand, TenantSpecializationDto>
{
    public async Task<ErrorOr<TenantSpecializationDto>> Handle(
        CreateTenantSpecializationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing specializations.");
        }

        var title = request.Title.Trim();
        var titleExists = await specializationRepository.ExistsAsync(
            filter: entry => entry.Title.ToLower() == title.ToLower(),
            cancellationToken: cancellationToken);
        if (titleExists)
        {
            return Error.Conflict(
                "TenantSpecialization.TitleAlreadyExists",
                "A specialization with this title already exists.");
        }

        var specialization = new TenantSpecialization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DefaultSkills = TenantUserSkillMerge.NormalizeSkills(request.DefaultSkills),
        };

        await specializationRepository.AddAsync(specialization, cancellationToken);
        return GetTenantSpecializationsQueryHandler.MapToDto(specialization);
    }
}
