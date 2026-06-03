using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Users.UpdateUserProfile;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Users.DeleteUserProfileImage;

public sealed class DeleteUserProfileImageCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IProfileImageService profileImages,
    IUrlService urlService)
    : ICommandHandler<DeleteUserProfileImageCommand, UpdateUserProfileResponse>
{
    public async Task<ErrorOr<UpdateUserProfileResponse>> Handle(
        DeleteUserProfileImageCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Unauthenticated", "Authentication required.");
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var previous = user.PhotoBlobId;
        if (string.IsNullOrEmpty(previous))
        {
            return new UpdateUserProfileResponse
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                AvatarUrl = urlService.GetUserProfilePictureUrl(user.PhotoBlobId),
            };
        }

        user.PhotoBlobId = null;
        var update = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!update.Succeeded)
        {
            return Error.Validation(
                "User.UpdateFailed",
                string.Join(' ', update.Errors.Select(e => e.Description)));
        }

        try
        {
            await profileImages.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            /* best-effort cleanup */
        }

        return new UpdateUserProfileResponse
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = urlService.GetUserProfilePictureUrl(user.PhotoBlobId),
        };
    }
}
