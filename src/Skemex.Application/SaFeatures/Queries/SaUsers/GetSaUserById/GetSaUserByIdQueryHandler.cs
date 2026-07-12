using ErrorOr;
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

namespace Skemex.Application.SaFeatures.Queries.SaUsers.GetSaUserById;

public sealed class GetSaUserByIdQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<User> userRepository,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : IQueryHandler<GetSaUserByIdQuery, SaUserDto>
{
    public async Task<ErrorOr<SaUserDto>> Handle(
        GetSaUserByIdQuery request,
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
            return Error.NotFound("User.NotFound", "User was not found.");
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
