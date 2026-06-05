using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Infrastructure.Authentication.Models;
using Skemex.Infrastructure.Authentication;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginHandler(
    UserManager<User> userManager,
    TokenService tokenService,
    IUrlService urlService,
    IBaseRepository<User> userRepository)
    : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<ErrorOr<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        if (!user.EmailConfirmed)
        {
            return Error.Unauthorized(UserErrors.EmailUnverified, UserErrors.EmailUnverifiedDescription);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Error.Unauthorized(UserErrors.InvalidPassword, UserErrors.InvalidPasswordDescription);
        }

        var token = await tokenService.GenerateGeneralLoginToken(user);
        token.RefreshToken = tokenService.GenerateRefreshToken();

        user.RefreshToken = token.RefreshToken;
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(2);

        await userManager.UpdateAsync(user);

        var userFromRepo = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q
                .Include(u => u.Tenants)
                .ThenInclude(tu => tu.Tenant)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role),
            cancellationToken);

        if (userFromRepo is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var isSuperAdmin = userFromRepo.UserRoles.Any(ur =>
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
            .Select(tu => new TenantSummaryResponse
            {
                Id = tu.TenantId,
                Name = tu.Tenant.Name,
                LogoUrl = urlService.GetTenantLogoUrl(tu.Tenant.LogoBlobId),
                Status = tu.Status,
            })
            .ToList();

        var currentUser = new CurrentUserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = userFromRepo.FirstName,
            LastName = userFromRepo.LastName,
            IsSuperAdmin = isSuperAdmin,
            AvatarUrl = await urlService.GetUserProfilePictureUrlAsync(userFromRepo.PhotoBlobId, cancellationToken)
                .ConfigureAwait(false),
            Tenants = tenants,
            Roles = platformRoles,
            Permissions = [],
        };

        return new LoginResponse
        {
            Token = token,
            User = currentUser,
        };
    }
}
