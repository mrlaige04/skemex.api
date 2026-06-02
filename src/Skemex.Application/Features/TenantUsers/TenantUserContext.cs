using ErrorOr;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.TenantUsers;

internal static class TenantUserContext
{
    public static ErrorOr<Guid> RequireTenantId(ICurrentUser currentUser)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden(
                "Tenant.Required",
                "Select a workspace before managing users.");
        }

        return tenantId.Value;
    }
}
