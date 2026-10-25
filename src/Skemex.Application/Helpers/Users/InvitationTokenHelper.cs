using System.Security.Cryptography;
using Skemex.Domain.Consts;

namespace Skemex.Application.Helpers.Users;

public static class InvitationTokenHelper
{
    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static DateTimeOffset ExpiresAt() =>
        DateTimeOffset.UtcNow.AddDays(InvitationConsts.ExpiryDays);
}