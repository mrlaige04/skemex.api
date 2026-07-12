using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Users.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IProfileImageService profileImages,
    IUrlService urlService)
    : ICommandHandler<UpdateUserProfileCommand, UpdateUserProfileResponse>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    public async Task<ErrorOr<UpdateUserProfileResponse>> Handle(
        UpdateUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await HandleCore(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            request.ProfileImage?.Dispose();
        }
    }

    private async Task<ErrorOr<UpdateUserProfileResponse>> HandleCore(
        UpdateUserProfileCommand request,
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

        if (request.ProfileImage is not null)
        {
            if (!AllowedContentTypes.Contains(request.ProfileImageContentType ?? string.Empty))
            {
                return Error.Validation(
                    "Profile.InvalidImageType",
                    "Image must be JPEG, PNG, WebP, or GIF.");
            }

            if (request.ProfileImage is not MemoryStream && !request.ProfileImage.CanSeek)
            {
                return Error.Validation("Profile.ImageNotReadable", "Could not read the uploaded image.");
            }

            try
            {
                if (request.ProfileImage.Length > MaxImageBytes)
                {
                    return Error.Validation("Profile.ImageTooLarge", "Image must be at most 5 MB.");
                }
            }
            catch (NotSupportedException)
            {
                return Error.Validation("Profile.ImageNotReadable", "Could not read the uploaded image.");
            }
        }

        var changed = false;

        if (request.FirstName is not null)
        {
            var t = request.FirstName.Trim();
            if (t.Length > 0 && t != user.FirstName)
            {
                user.FirstName = t;
                changed = true;
            }
        }

        if (request.LastName is not null)
        {
            var t = request.LastName.Trim();
            if (t.Length > 0 && t != user.LastName)
            {
                user.LastName = t;
                changed = true;
            }
        }

        if (request.ProfileImage is not null)
        {
            request.ProfileImage.Position = 0;
            var contentType = request.ProfileImageContentType ?? "application/octet-stream";
            var blobId = await profileImages.ReplaceAsync(
                    user.Id,
                    user.PhotoBlobId,
                    request.ProfileImage,
                    contentType,
                    request.ProfileImageFileName,
                    cancellationToken)
                .ConfigureAwait(false);

            user.PhotoBlobId = blobId;
            changed = true;
        }

        if (changed)
        {
            var update = await userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!update.Succeeded)
            {
                return Error.Validation(
                    "User.UpdateFailed",
                    string.Join(' ', update.Errors.Select(e => e.Description)));
            }
        }

        return new UpdateUserProfileResponse
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = await urlService.GetUserProfilePictureUrlAsync(user.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
        };
    }
}
