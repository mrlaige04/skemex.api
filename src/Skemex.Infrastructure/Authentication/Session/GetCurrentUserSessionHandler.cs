using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Common;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Session;

public sealed class GetCurrentUserSessionHandler(
    ICurrentUser currentUser,
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
                .ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
                .ThenInclude(p => p.Group),
            cancellationToken);

        if (userFromRepo is null)
        {
            return Error.NotFound("User.NotFound", "User was not found.");
        }

        var isPlatformAdmin = superAdminOptions.Value.MatchesEmail(userFromRepo.Email);
        var isSuperAdmin = isPlatformAdmin || userFromRepo.UserRoles.Any(ur =>
            ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin);

        var tenantId = currentUser.GetTenantId();

        var roles = userFromRepo.UserRoles
            .Where(ur =>
                tenantId is null
                    ? ur.TenantId is null
                    : ur.TenantId == tenantId || (ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin))
            .Select(ur => new RoleBaseResponse
            {
                Id = ur.RoleId,
                Name = ur.Role.Name!,
                IsSystem = ur.Role.IsSystem,
            })
            .DistinctBy(r => r.Id)
            .ToList();

        var permissions = tenantId is null
            ? []
            : userFromRepo.UserRoles
                .Where(ur => ur.TenantId == tenantId)
                .SelectMany(ur => ur.Role.Permissions)
                .Where(rp => rp.Permission is not null && !string.IsNullOrWhiteSpace(rp.Permission.Name))
                .Select(rp => new PermissionBaseResponse
                {
                    Id = rp.PermissionId,
                    Name = rp.Permission.Name,
                    GroupName = rp.Permission.Group?.Name ?? string.Empty,
                    IsSystem = rp.Permission.IsSystem,
                })
                .DistinctBy(p => p.Name)
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
            Roles = roles,
            Permissions = permissions,
        };
    }
}
