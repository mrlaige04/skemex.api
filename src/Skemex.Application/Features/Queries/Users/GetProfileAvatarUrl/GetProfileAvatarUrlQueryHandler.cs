using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Users.GetProfileAvatarUrl;

public sealed class GetProfileAvatarUrlQueryHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IProfileImageService profileImages)
    : IQueryHandler<GetProfileAvatarUrlQuery, ProfileAvatarUrlResponse>
{
    public async Task<ErrorOr<ProfileAvatarUrlResponse>> Handle(
        GetProfileAvatarUrlQuery query,
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

        return new ProfileAvatarUrlResponse
        {
            AvatarUrl = await profileImages.GetAvatarUrlAsync(user.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
        };
    }
}
