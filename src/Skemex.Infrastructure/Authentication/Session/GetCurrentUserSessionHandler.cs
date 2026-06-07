using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Session;

public sealed class GetCurrentUserSessionHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    IUrlService urlService,
    IBaseRepository<User> userRepository,
    IOptions<SuperAdminOptions> superAdminOptions)
    : IQueryHandler<GetCurrentUserSessionQuery, CurrentUserResponse>
{
    public async Task<ErrorOr<CurrentUserResponse>> Handle(
        GetCurrentUserSessionQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Unauthenticated", "Authentication required.");
        }

        var userFromRepo = await userRepository.GetAsync(
            u => u.Id == userId,
            q => q
                .Include(u => u.Tenants)
                .ThenInclude(tu => tu.Tenant)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role),
            cancellationToken);

        if (userFromRepo is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var isPlatformAdmin = superAdminOptions.Value.MatchesEmail(userFromRepo.Email);
        var isSuperAdmin = isPlatformAdmin || userFromRepo.UserRoles.Any(ur =>
            ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin);

        var platformRoles = userFromRepo.UserRoles
            .Where(ur => ur.TenantId is null)
            .Select(ur => new RoleBaseResponse
            {
                Id = ur.RoleId,
                Name = ur.Role.Name!,
                IsSystem = ur.Role.IsSystem,
            })
            .ToList();

        var tenants = userFromRepo.Tenants
            .Where(tu => tu.Status == TenantUserStatus.Active)
            .Select(tu => new TenantSummaryResponse
            {
                Id = tu.TenantId,
                Name = tu.Tenant.Name,
                LogoUrl = urlService.GetTenantLogoUrl(tu.Tenant.LogoBlobId),
                Status = tu.Status,
            })
            .ToList();

        return new CurrentUserResponse
        {
            Id = userFromRepo.Id,
            Email = userFromRepo.Email ?? string.Empty,
            FirstName = userFromRepo.FirstName,
            LastName = userFromRepo.LastName,
            IsSuperAdmin = isSuperAdmin,
            AvatarUrl = await urlService.GetUserProfilePictureUrlAsync(userFromRepo.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
            Tenants = tenants,
            Roles = platformRoles,
            Permissions = [],
        };
    }
}
