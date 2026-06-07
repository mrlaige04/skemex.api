using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.TenantUsers;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.CreateSaUser;

public sealed class CreateSaUserCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : ICommandHandler<CreateSaUserCommand, SaUserDto>
{
    public async Task<ErrorOr<SaUserDto>> Handle(
        CreateSaUserCommand request,
        CancellationToken cancellationToken)
    {
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (superAdminOptions.Value.MatchesEmail(email))
        {
            return Error.Validation(
                TenantUserErrors.SuperAdminEmailReserved,
                TenantUserErrors.SuperAdminEmailReservedDescription);
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Error.Conflict(
                SaUserErrors.EmailAlreadyExists,
                SaUserErrors.EmailAlreadyExistsDescription);
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = true,
        };

        var password = request.Password?.Trim();
        IdentityResult createResult = string.IsNullOrEmpty(password)
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            if (createResult.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Error.Conflict(
                    SaUserErrors.EmailAlreadyExists,
                    SaUserErrors.EmailAlreadyExistsDescription);
            }

            return Error.Validation(
                SaUserErrors.CreateFailed,
                string.Join(' ', createResult.Errors.Select(e => e.Description)));
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
            WorkspaceCount = 0,
        };
    }
}
