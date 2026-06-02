using Skemex.Application.Features.Abstractions;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.RefreshToken;

public sealed class RefreshTokenCommand : ICommand<AccessTokenResponse>
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
