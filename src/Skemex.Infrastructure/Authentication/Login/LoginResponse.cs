using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginResponse
{
    public AccessTokenResponse Token { get; set; } = null!;
    public CurrentUserResponse User { get; set; } = null!;
}
