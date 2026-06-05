using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Authentication.Models;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.SelectTenant;

public class SelectTenantHandler(
    ICurrentUser currentUser,
    UserManager<User> userManager,
    TokenService tokenService,
    IBaseRepository<User> userRepository,
    IBaseRepository<Tenant> tenantRepository)
    : ICommandHandler<SelectTenantCommand, TenantSessionResponse>
{
    public async Task<ErrorOr<TenantSessionResponse>> Handle(
        SelectTenantCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Unauthenticated", "Authentication required.");
        }

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var tenant = await tenantRepository.GetAsync(t => t.Id == request.TenantId, cancellationToken: cancellationToken);
        if (tenant is null)
        {
            return Error.NotFound(UserErrors.TenantAccessDenied, UserErrors.TenantAccessDeniedDescription);
        }

        var principal = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q
                .Include(u => u.Tenants)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
                .ThenInclude(p => p.Group),
            cancellationToken);

        if (principal is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var isSuperAdmin = principal.UserRoles.Any(ur =>
            ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin);

        if (!isSuperAdmin)
        {
            var membership = principal.Tenants.FirstOrDefault(tu => tu.TenantId == request.TenantId);
            if (membership is null || membership.Status != TenantUserStatus.Active)
            {
                return Error.Forbidden(UserErrors.TenantAccessDenied, UserErrors.TenantAccessDeniedDescription);
            }
        }

        var token = await tokenService.GenerateTenantScopedToken(user, request.TenantId);
        token.RefreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = token.RefreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(2);
        await userManager.UpdateAsync(user);

        var rolesForTenant = principal.UserRoles
            .Where(ur => ur.TenantId == request.TenantId || (ur.TenantId is null && ur.Role.Name == RoleNames.SuperAdmin))
            .Select(ur => new RoleBaseResponse
            {
                Id = ur.RoleId,
                Name = ur.Role.Name!,
                IsSystem = ur.Role.IsSystem,
            })
            .DistinctBy(r => r.Id)
            .ToList();

        var permissions = principal.UserRoles
            .Where(ur => ur.TenantId == request.TenantId)
            .SelectMany(ur => ur.Role.Permissions)
            .Select(rp => new PermissionBaseResponse
            {
                Id = rp.PermissionId,
                Name = rp.Permission.Name,
                GroupName = rp.Permission.Group.Name,
                IsSystem = rp.Permission.IsSystem,
            })
            .DistinctBy(p => p.Name)
            .ToList();

        return new TenantSessionResponse
        {
            Token = token,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Roles = rolesForTenant,
            Permissions = permissions,
        };
    }
}
