using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaUser;

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
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
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
            return Error.NotFound(SaUserErrors.NotFound, SaUserErrors.NotFoundDescription);
        }

        if (SaUserRules.IsSuperAdminUser(user, superAdminOptions.Value))
        {
            return Error.Validation(
                SaUserErrors.SuperAdminProtected,
                SaUserErrors.SuperAdminProtectedDescription);
        }

        var delete = await userManager.DeleteAsync(user);
        if (!delete.Succeeded)
        {
            return Error.Validation(
                SaUserErrors.DeleteFailed,
                string.Join(' ', delete.Errors.Select(e => e.Description)));
        }

        return Result.Success;
    }
}
