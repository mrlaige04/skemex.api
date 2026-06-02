using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Web.Attributes;

public class RoleFilter(
    ICurrentUser currentUser,
    IBaseRepository<User> userRepository,
    string role) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = currentUser.GetUserId();
        if (userId == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var rolesFromJwt = currentUser.GetRoles();
        var userEntity = await userRepository.GetAsync(
            u => u.Id == userId,
            q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role));

        var globalSuper = userEntity?.UserRoles.Any(ur =>
            ur.Role.Name == RoleNames.SuperAdmin && ur.TenantId == null) ?? false;

        var tenantId = currentUser.GetTenantId();
        var hasDbRole = globalSuper || (userEntity?.UserRoles.Any(ur =>
            ur.Role.Name == role &&
            (ur.TenantId == null || ur.TenantId == tenantId)) ?? false);

        var jwtHasRole = rolesFromJwt?.Any(r => r == role) ?? false;
        var jwtIsSuper = rolesFromJwt?.Any(r => r == RoleNames.SuperAdmin) ?? false;

        var hasAccess = hasDbRole && (jwtHasRole || (jwtIsSuper && globalSuper));
        if (!hasAccess)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
