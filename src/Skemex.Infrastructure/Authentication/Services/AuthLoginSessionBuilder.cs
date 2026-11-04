using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.App;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Users;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication.Login;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Services;

/// <summary>Builds the same <see cref="LoginResponse"/> used by password and external login.</summary>
public sealed class AuthLoginSessionBuilder(
    UserManager<User> userManager,
    TokenService tokenService,
    IUrlService urlService,
    IBaseRepository<User> userRepository,
    TenantProvisioningService tenantProvisioning,
    IOptions<SuperAdminOptions> superAdminOptions)
{
    public async Task<ErrorOr<LoginResponse>> BuildAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var userFromRepo = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q
                .Include(u => u.Tenants)
                .ThenInclude(tu => tu.Tenant)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
                .ThenInclude(p => p.Group),
            cancellationToken).ConfigureAwait(false);

        if (userFromRepo is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var isPlatformAdmin = superAdminOptions.Value.MatchesEmail(user.Email);
        var isSuperAdmin = isPlatformAdmin || userFromRepo.UserRoles.Any(ur =>
            ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin);

        if (isSuperAdmin)
        {
            var token = await tokenService.GenerateGeneralLoginToken(user).ConfigureAwait(false);
            tokenService.AssignRefreshToken(user, token);
            await userManager.UpdateAsync(user).ConfigureAwait(false);

            var platformRoles = userFromRepo.UserRoles
                .Where(ur => ur.TenantId is null)
                .Select(ur => new RoleBaseResponse
                {
                    Id = ur.RoleId,
                    Name = ur.Role.Name!,
                    IsSystem = ur.Role.IsSystem,
                })
                .ToList();

            return new LoginResponse
            {
                Token = token,
                TenantId = null,
                TenantName = null,
                User = new CurrentUserResponse
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = userFromRepo.FirstName,
                    LastName = userFromRepo.LastName,
                    IsSuperAdmin = true,
                    AvatarUrl = await urlService
                        .GetUserProfilePictureUrlAsync(userFromRepo.PhotoBlobId, cancellationToken)
                        .ConfigureAwait(false),
                    Tenants = [],
                    Roles = platformRoles,
                    Permissions = [],
                },
            };
        }

        var resolved = await ResolveOrProvisionTenantAsync(user, userFromRepo, cancellationToken)
            .ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Errors;
        }

        var (tenantId, tenantName, memberships) = resolved.Value;

        var tokenScoped = await tokenService.GenerateTenantScopedToken(user, tenantId).ConfigureAwait(false);
        tokenService.AssignRefreshToken(user, tokenScoped);
        user.LastUsedTenantId = tenantId;
        await userManager.UpdateAsync(user).ConfigureAwait(false);

        // Reload roles/permissions after possible personal-workspace provision.
        if (memberships.All(tu => tu.TenantId != tenantId)
            || userFromRepo.UserRoles.All(ur => ur.TenantId != tenantId))
        {
            userFromRepo = await userRepository.GetAsync(
                u => u.Id == user.Id,
                q => q
                    .Include(u => u.Tenants)
                    .ThenInclude(tu => tu.Tenant)
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
                    .ThenInclude(rp => rp.Permission)
                    .ThenInclude(p => p.Group),
                cancellationToken).ConfigureAwait(false);

            if (userFromRepo is null)
            {
                return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
            }

            memberships = userFromRepo.Tenants.Where(tu => tu.Status == TenantUserStatus.Active).ToList();
        }

        var rolesForTenant = userFromRepo.UserRoles
            .Where(ur => ur.TenantId == tenantId)
            .Select(ur => new RoleBaseResponse
            {
                Id = ur.RoleId,
                Name = ur.Role.Name!,
                IsSystem = ur.Role.IsSystem,
            })
            .DistinctBy(r => r.Id)
            .ToList();

        var permissions = userFromRepo.UserRoles
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

        var tenants = memberships
            .Select(tu => new TenantSummaryResponse
            {
                Id = tu.TenantId,
                Name = tu.Tenant.Name,
                LogoUrl = urlService.GetTenantLogoUrl(tu.Tenant.LogoBlobId),
                Status = tu.Status,
            })
            .ToList();

        if (tenants.All(t => t.Id != tenantId))
        {
            tenants.Add(new TenantSummaryResponse
            {
                Id = tenantId,
                Name = tenantName,
                Status = TenantUserStatus.Active,
            });
        }

        return new LoginResponse
        {
            Token = tokenScoped,
            TenantId = tenantId,
            TenantName = tenantName,
            User = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = userFromRepo.FirstName,
                LastName = userFromRepo.LastName,
                IsSuperAdmin = false,
                AvatarUrl = await urlService
                    .GetUserProfilePictureUrlAsync(userFromRepo.PhotoBlobId, cancellationToken)
                    .ConfigureAwait(false),
                Tenants = tenants,
                Roles = rolesForTenant,
                Permissions = permissions,
            },
        };
    }

    /// <summary>
    /// Resolves last-used or first active tenant; provisions a personal workspace when the user has none.
    /// </summary>
    public async Task<ErrorOr<Guid>> ResolveTenantIdForUserAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var userFromRepo = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q.Include(u => u.Tenants).ThenInclude(tu => tu.Tenant),
            cancellationToken).ConfigureAwait(false);

        if (userFromRepo is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var resolved = await ResolveOrProvisionTenantAsync(user, userFromRepo, cancellationToken)
            .ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Errors;
        }

        return resolved.Value.TenantId;
    }

    private async Task<ErrorOr<(Guid TenantId, string TenantName, List<TenantUser> Memberships)>> ResolveOrProvisionTenantAsync(
        User user,
        User userFromRepo,
        CancellationToken cancellationToken)
    {
        var memberships = userFromRepo.Tenants
            .Where(tu => tu.Status == TenantUserStatus.Active)
            .ToList();

        if (user.LastUsedTenantId is Guid lastId)
        {
            var last = memberships.FirstOrDefault(tu => tu.TenantId == lastId);
            if (last is not null)
            {
                return (last.TenantId, last.Tenant.Name, memberships);
            }
        }

        var first = memberships.FirstOrDefault();
        if (first is not null)
        {
            return (first.TenantId, first.Tenant.Name, memberships);
        }

        var provisioned = await tenantProvisioning.CreatePersonalWorkspaceAsync(user, cancellationToken)
            .ConfigureAwait(false);
        if (provisioned.IsError)
        {
            return provisioned.Errors;
        }

        var tenant = provisioned.Value;
        return (tenant.Id, tenant.Name, memberships);
    }
}
