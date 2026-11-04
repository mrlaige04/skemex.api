using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginResponse
{
    public AccessTokenResponse Token { get; set; } = null!;
    public CurrentUserResponse User { get; set; } = null!;

    /// <summary>Selected workspace for non–super-admin logins; null for platform admins.</summary>
    public Guid? TenantId { get; set; }

    public string? TenantName { get; set; }
}
