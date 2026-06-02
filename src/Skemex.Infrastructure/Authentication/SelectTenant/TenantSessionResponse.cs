using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.SelectTenant;

public class TenantSessionResponse
{
    public AccessTokenResponse Token { get; set; } = null!;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = null!;
    public IList<RoleBaseResponse> Roles { get; set; } = [];
    public IList<PermissionBaseResponse> Permissions { get; set; } = [];
}
