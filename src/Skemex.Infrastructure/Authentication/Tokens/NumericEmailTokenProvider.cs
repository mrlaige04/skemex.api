using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Skemex.Infrastructure.Authentication.Tokens;

public sealed class NumericEmailTokenProvider<TUser> : IUserTwoFactorTokenProvider<TUser>
    where TUser : class
{
    public const string ProviderName = "NumericEmail";

    private readonly NumericEmailTokenProviderOptions _options;

    public NumericEmailTokenProvider(IOptions<NumericEmailTokenProviderOptions> options)
    {
        _options = options.Value;
    }

    public string Name => ProviderName;

    public async Task<string> GenerateAsync(string purpose, UserManager<TUser> manager, TUser user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        await StoreCodeAsync(manager, user, purpose, code).ConfigureAwait(false);
        return code;
    }

    public async Task<bool> ValidateAsync(
        string purpose,
        string token,
        UserManager<TUser> manager,
        TUser user)
    {
        var stored = await manager.GetAuthenticationTokenAsync(user, Name, purpose).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split(':', 2);
        if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return false;
        }

        var created = DateTimeOffset.FromUnixTimeSeconds(unix);
        if (created.Add(_options.TokenLifespan) < DateTimeOffset.UtcNow)
        {
            return false;
        }

        return string.Equals(parts[0], token.Trim(), StringComparison.Ordinal);
    }

    public async Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
    {
        if (!manager.SupportsUserEmail)
        {
            return false;
        }

        var email = await manager.GetEmailAsync(user).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(email);
    }

    private static Task StoreCodeAsync(UserManager<TUser> manager, TUser user, string purpose, string code)
    {
        var payload = $"{code}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return manager.SetAuthenticationTokenAsync(user, ProviderName, purpose, payload);
    }
}
