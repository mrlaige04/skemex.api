using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.Users.GetTenantUserById;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantSpecializations.AssignTenantSpecialization;

public sealed class AssignTenantSpecializationCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository,
    ITenantRepository<TenantUser> tenantUserRepository,
    ISender sender)
    : ICommandHandler<AssignTenantSpecializationCommand, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        AssignTenantSpecializationCommand request,
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

        var tenantUser = await tenantUserRepository.GetAsync(
            filter: entry => entry.UserId == request.UserId,
            include: query => query
                .Include(entry => entry.Specializations)
                .Include(entry => entry.User),
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("User.NotFound", "User was not found in this workspace.");
        }

        var alreadyAssigned = tenantUser.Specializations
            .Any(entry => entry.TenantSpecializationId == specialization.Id);
        if (!alreadyAssigned)
        {
            tenantUser.Specializations.Add(new TenantUserSpecialization
            {
                TenantUserId = tenantUser.Id,
                TenantSpecializationId = specialization.Id,
            });
        }

        TenantUserSkillMerge.MergeDefaultSkills(tenantUser, specialization.DefaultSkills);
        await tenantUserRepository.UpdateAsync(tenantUser, cancellationToken);

        return await sender.Send(
            new GetTenantUserByIdQuery { UserId = request.UserId },
            cancellationToken);
    }
}
