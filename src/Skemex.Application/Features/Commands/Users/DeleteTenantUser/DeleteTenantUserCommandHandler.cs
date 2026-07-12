using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Users.DeleteTenantUser;

public sealed class DeleteTenantUserCommandHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository)
    : ICommandHandler<DeleteTenantUserCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteTenantUserCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        var currentUserId = currentUser.GetUserId();
        if (currentUserId == request.UserId)
        {
            return Error.Validation("User.CannotDeleteSelf", "You cannot remove yourself from the workspace.");
        }

        var tenantUser = await tenantUserRepository.GetAsync(
            filter: tu => tu.UserId == request.UserId && tu.TenantId == tenantId,
            cancellationToken: cancellationToken);

        if (tenantUser is null)
        {
            return Error.NotFound("User.NotFound", "User was not found in this workspace.");
        }

        var userRoles = await userRoleRepository.GetAllAsync(
            filter: ur => ur.TenantId == tenantId && ur.UserId == request.UserId,
            cancellationToken: cancellationToken);

        if (userRoles.Count > 0)
        {
            await userRoleRepository.DeleteRangeAsync(userRoles, cancellationToken);
        }

        await tenantUserRepository.DeleteAsync(tenantUser, cancellationToken);

        return Result.Success;
    }
}
