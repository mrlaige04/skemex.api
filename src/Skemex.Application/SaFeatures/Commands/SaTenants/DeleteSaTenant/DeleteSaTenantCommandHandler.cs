using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.DeleteSaTenant;

public sealed class DeleteSaTenantCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Tenant> tenantRepository)
    : ICommandHandler<DeleteSaTenantCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteSaTenantCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var tenant = await tenantRepository.GetAsync(
            filter: t => t.Id == request.TenantId,
            cancellationToken: cancellationToken);

        if (tenant is null)
        {
            return Error.NotFound("Tenant.NotFound", "Workspace was not found.");
        }

        await tenantRepository.DeleteAsync(tenant, cancellationToken);

        return Result.Success;
    }
}
