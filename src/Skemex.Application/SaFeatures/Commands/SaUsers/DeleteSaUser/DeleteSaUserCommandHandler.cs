using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Domain.Consts;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.DeleteSaUser;

public sealed class DeleteSaUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IBaseRepository<User> userRepository,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<DeleteSaUserCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteSaUserCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var currentUserId = currentUser.GetUserId();
        if (currentUserId == request.UserId)
        {
            return Error.Validation("User.CannotDeleteSelf", "You cannot delete your own account.");
        }

        var user = await userRepository.GetAsync(
            filter: u => u.Id == request.UserId,
            include: q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role),
            cancellationToken: cancellationToken);

        if (user is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        if (superAdminOptions.Value.MatchesEmail(user.Email) ||
            user.UserRoles.Any(ur =>
                ur.TenantId is null &&
                string.Equals(ur.Role?.Name, RoleNames.SuperAdmin, StringComparison.Ordinal)))
        {
            return Error.Validation(
                "User.SuperAdminProtected",
                "The platform super-admin account cannot be managed here.");
        }

        var delete = await userManager.DeleteAsync(user);
        if (!delete.Succeeded)
        {
            return Error.Validation(
                "User.DeleteFailed",
                string.Join(' ', delete.Errors.Select(e => e.Description)));
        }

        return Result.Success;
    }
}
