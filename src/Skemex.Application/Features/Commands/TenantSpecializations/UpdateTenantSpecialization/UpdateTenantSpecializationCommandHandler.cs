using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.TenantSpecializations.GetTenantSpecializations;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantSpecializations.UpdateTenantSpecialization;

public sealed class UpdateTenantSpecializationCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository)
    : ICommandHandler<UpdateTenantSpecializationCommand, TenantSpecializationDto>
{
    public async Task<ErrorOr<TenantSpecializationDto>> Handle(
        UpdateTenantSpecializationCommand request,
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

        var changed = false;

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length == 0)
            {
                return Error.Validation(
                    "TenantSpecialization.InvalidTitle",
                    "Specialization title cannot be empty.");
            }

            if (!string.Equals(title, specialization.Title, StringComparison.Ordinal))
            {
                var titleExists = await specializationRepository.ExistsAsync(
                    filter: entry =>
                        entry.Id != specialization.Id && entry.Title.ToLower() == title.ToLower(),
                    cancellationToken: cancellationToken);
                if (titleExists)
                {
                    return Error.Conflict(
                        "TenantSpecialization.TitleAlreadyExists",
                        "A specialization with this title already exists.");
                }

                specialization.Title = title;
                changed = true;
            }
        }

        if (request.Description is not null)
        {
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();
            if (description != specialization.Description)
            {
                specialization.Description = description;
                changed = true;
            }
        }

        if (request.DefaultSkills is not null)
        {
            var skills = TenantUserSkillMerge.NormalizeSkills(request.DefaultSkills);
            if (!SkillsEqual(specialization.DefaultSkills, skills))
            {
                specialization.DefaultSkills = skills;
                changed = true;
            }
        }

        if (changed)
        {
            await specializationRepository.UpdateAsync(specialization, cancellationToken);
        }

        return GetTenantSpecializationsQueryHandler.MapToDto(specialization);
    }

    private static bool SkillsEqual(IEnumerable<string>? left, IReadOnlyList<string> right)
    {
        var leftList = left?.ToList() ?? [];
        if (leftList.Count != right.Count)
        {
            return false;
        }

        return leftList
            .Zip(right)
            .All(pair => string.Equals(pair.First, pair.Second, StringComparison.Ordinal));
    }
}
