using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.TenantSpecializations.DeleteTenantSpecialization;

public sealed class DeleteTenantSpecializationCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantSpecialization> specializationRepository)
    : ICommandHandler<DeleteTenantSpecializationCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteTenantSpecializationCommand request,
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

        await specializationRepository.DeleteAsync(specialization, cancellationToken);
        return Result.Success;
    }
}
