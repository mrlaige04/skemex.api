using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
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
    IOptions<SuperAdminOptions> superAdminOptions)
{
    public async Task<ErrorOr<LoginResponse>> BuildAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var token = await tokenService.GenerateGeneralLoginToken(user).ConfigureAwait(false);
        tokenService.AssignRefreshToken(user, token);
        await userManager.UpdateAsync(user).ConfigureAwait(false);

        var userFromRepo = await userRepository.GetAsync(
            u => u.Id == user.Id,
            q => q
                .Include(u => u.Tenants)
                .ThenInclude(tu => tu.Tenant)
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role),
            cancellationToken).ConfigureAwait(false);

        if (userFromRepo is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        var isPlatformAdmin = superAdminOptions.Value.MatchesEmail(user.Email);
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
            .Select(tu => new TenantSummaryResponse
            {
                Id = tu.TenantId,
                Name = tu.Tenant.Name,
                LogoUrl = urlService.GetTenantLogoUrl(tu.Tenant.LogoBlobId),
                Status = tu.Status,
            })
            .ToList();

        return new LoginResponse
        {
            Token = token,
            User = new CurrentUserResponse
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = userFromRepo.FirstName,
                LastName = userFromRepo.LastName,
                IsSuperAdmin = isSuperAdmin,
                AvatarUrl = await urlService
                    .GetUserProfilePictureUrlAsync(userFromRepo.PhotoBlobId, cancellationToken)
                    .ConfigureAwait(false),
                Tenants = tenants,
                Roles = platformRoles,
                Permissions = [],
            },
        };
    }
}
