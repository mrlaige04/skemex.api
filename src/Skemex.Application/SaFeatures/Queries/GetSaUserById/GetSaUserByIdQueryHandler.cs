using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.GetSaUserById;

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
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
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
            return Error.NotFound(SaUserErrors.NotFound, SaUserErrors.NotFoundDescription);
        }

        if (SaUserRules.IsSuperAdminUser(user, superAdminOptions.Value))
        {
            return Error.NotFound(SaUserErrors.NotFound, SaUserErrors.NotFoundDescription);
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
