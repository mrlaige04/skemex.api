using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Features.Abstractions;
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
using Skemex.Application.SaModels.SaUsers;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.CreateSaUser;

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
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (superAdminOptions.Value.MatchesEmail(email))
        {
            return Error.Validation(
                "User.SuperAdminEmailReserved",
                "This email address is reserved and cannot be used.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Error.Conflict(
                "User.EmailAlreadyExists",
                "An account with this email already exists.");
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
                    "User.EmailAlreadyExists",
                    "An account with this email already exists.");
            }

            return Error.Validation(
                "User.CreateFailed",
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
