using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.SaModels.SaUsers;
using Skemex.Domain.Consts;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.UpdateSaUser;

public sealed class UpdateSaUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IBaseRepository<User> userRepository,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<UpdateSaUserCommand, SaUserDto>
{
    public async Task<ErrorOr<SaUserDto>> Handle(
        UpdateSaUserCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var user = await userRepository.GetAsync(
            filter: u => u.Id == request.UserId,
            include: q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.Tenants),
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

        var changed = false;

        if (request.FirstName is not null)
        {
            var firstName = request.FirstName.Trim();
            if (firstName.Length > 0 && firstName != user.FirstName)
            {
                user.FirstName = firstName;
                changed = true;
            }
        }

        if (request.LastName is not null)
        {
            var lastName = request.LastName.Trim();
            if (lastName.Length > 0 && lastName != user.LastName)
            {
                user.LastName = lastName;
                changed = true;
            }
        }

        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (email.Length > 0 && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                if (superAdminOptions.Value.MatchesEmail(email))
                {
                    return Error.Validation(
                        "User.SuperAdminEmailReserved",
                        "This email address is reserved and cannot be used.");
                }

                var taken = await userManager.FindByEmailAsync(email);
                if (taken is not null && taken.Id != user.Id)
                {
                    return Error.Conflict(
                        "User.EmailAlreadyExists",
                        "An account with this email already exists.");
                }

                user.Email = email;
                user.UserName = email;
                user.NormalizedEmail = userManager.NormalizeEmail(email);
                user.NormalizedUserName = userManager.NormalizeName(email);
                changed = true;
            }
        }

        if (changed)
        {
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return Error.Validation(
                    "User.UpdateFailed",
                    string.Join(' ', update.Errors.Select(e => e.Description)));
            }
        }

        var avatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken).ConfigureAwait(false);

        return new SaUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            AvatarUrl = avatarUrl,
            WorkspaceCount = user.Tenants.Count,
        };
    }
}
