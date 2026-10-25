namespace Skemex.Infrastructure.Authentication.External;

public interface IExternalAuthProvider
{
    /// <summary>Stable provider key used in URLs and AspNetUserLogins (e.g. google, github).</summary>
    string Name { get; }

    string DisplayName { get; }

    bool IsConfigured { get; }

    Uri BuildAuthorizationUrl(string redirectUri, string state);

    Task<ExternalUserInfo> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default);
}

public sealed class ExternalUserInfo
{
    public required string Provider { get; init; }
    public required string Subject { get; init; }
    public required string Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class ExternalAuthProviderDto
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
}
