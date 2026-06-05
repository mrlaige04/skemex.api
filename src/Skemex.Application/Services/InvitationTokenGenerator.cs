using System.Security.Cryptography;

namespace Skemex.Application.Services;

public static class InvitationTokenGenerator
{
    public const int ExpiryDays = 7;

    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static DateTimeOffset ExpiresAt() =>
        DateTimeOffset.UtcNow.AddDays(ExpiryDays);
}
