using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
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
            AvatarUrl = await urlService.GetUserProfilePictureUrlAsync(user.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
        };
    }
}
