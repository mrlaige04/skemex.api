using ErrorOr;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures;

internal static class SaSuperAdminContext
{
    public static ErrorOr<Success> RequireSuperAdmin(ICurrentUser currentUser)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden(
                "SuperAdmin.Required",
                "Platform administrator access is required.");
        }

        return Result.Success;
    }
}
