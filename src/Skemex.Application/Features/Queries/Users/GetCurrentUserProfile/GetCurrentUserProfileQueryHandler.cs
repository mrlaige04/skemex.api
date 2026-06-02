using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Users.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileQueryHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IUrlService urlService)
    : IQueryHandler<GetCurrentUserProfileQuery, CurrentUserProfileResponse>
{
    public async Task<ErrorOr<CurrentUserProfileResponse>> Handle(
        GetCurrentUserProfileQuery query,
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

        return new CurrentUserProfileResponse
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            AvatarUrl = urlService.GetUserProfilePictureUrl(user.PhotoBlobId),
        };
    }
}
