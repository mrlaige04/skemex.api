using Skemex.Application.Configuration;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.SaFeatures;

internal static class SaUserRules
{
    public static bool IsSuperAdminUser(User user, SuperAdminOptions options)
    {
        if (options.MatchesEmail(user.Email))
        {
            return true;
        }

        return user.UserRoles.Any(ur =>
            ur.TenantId is null &&
            string.Equals(ur.Role?.Name, RoleNames.SuperAdmin, StringComparison.Ordinal));
    }
}
