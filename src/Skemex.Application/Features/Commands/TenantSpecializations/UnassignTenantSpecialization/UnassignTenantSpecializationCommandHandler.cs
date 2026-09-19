using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.Users.GetTenantUserById;
using Skemex.Application.Models.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantSpecializations.UnassignTenantSpecialization;

public sealed class UnassignTenantSpecializationCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository,
    ITenantRepository<TenantUser> tenantUserRepository,
    ISender sender)
    : ICommandHandler<UnassignTenantSpecializationCommand, TenantUserDto>
{
    public async Task<ErrorOr<TenantUserDto>> Handle(
        UnassignTenantSpecializationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing specializations.");
        }

        var specializationExists = await specializationRepository.ExistsAsync(
            filter: entry => entry.Id == request.SpecializationId,
            cancellationToken: cancellationToken);
        if (!specializationExists)
        {
            return Error.NotFound("TenantSpecialization.NotFound", "Specialization was not found.");
        }

        var tenantUser = await tenantUserRepository.GetAsync(
            filter: entry => entry.UserId == request.UserId,
            include: query => query.Include(entry => entry.Specializations),
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("User.NotFound", "User was not found in this workspace.");
        }

        var link = tenantUser.Specializations
            .FirstOrDefault(entry => entry.TenantSpecializationId == request.SpecializationId);
        if (link is not null)
        {
            tenantUser.Specializations.Remove(link);
            await tenantUserRepository.UpdateAsync(tenantUser, cancellationToken);
        }

        return await sender.Send(
            new GetTenantUserByIdQuery { UserId = request.UserId },
            cancellationToken);
    }
}
