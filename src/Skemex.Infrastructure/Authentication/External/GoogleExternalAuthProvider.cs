using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Skemex.Infrastructure.Authentication.External;

public sealed class GoogleExternalAuthProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ExternalAuthOptions> options) : IExternalAuthProvider
{
    public const string ProviderName = "google";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name => ProviderName;
    public string DisplayName => "Google";

    public bool IsConfigured => options.Value.Google.IsConfigured;

    public Uri BuildAuthorizationUrl(string redirectUri, string state)
    {
        var clientId = options.Value.Google.ClientId
            ?? throw new InvalidOperationException("Google OAuth ClientId is not configured.");

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "online",
            ["prompt"] = "select_account",
        };

        return new UriBuilder("https://accounts.google.com/o/oauth2/v2/auth")
        {
            Query = string.Join('&', query.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}")),
        }.Uri;
    }

    public async Task<ExternalUserInfo> ExchangeCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var google = options.Value.Google;
        var client = httpClientFactory.CreateClient(nameof(GoogleExternalAuthProvider));

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = google.ClientId!,
            ["client_secret"] = google.ClientSecret!,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });

        using var tokenResponse = await client
            .PostAsync("https://oauth2.googleapis.com/token", tokenRequest, cancellationToken)
            .ConfigureAwait(false);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google token exchange failed: {tokenJson}");
        }

        var token = JsonSerializer.Deserialize<GoogleTokenResponse>(tokenJson, JsonOptions)
            ?? throw new InvalidOperationException("Google token response was empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Google did not return an access token.");
        }

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://openidconnect.googleapis.com/v1/userinfo");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var userResponse = await client.SendAsync(userRequest, cancellationToken).ConfigureAwait(false);
        var userJson = await userResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!userResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Google userinfo failed: {userJson}");
        }

        var profile = JsonSerializer.Deserialize<GoogleUserInfo>(userJson, JsonOptions)
            ?? throw new InvalidOperationException("Google userinfo was empty.");

        if (string.IsNullOrWhiteSpace(profile.Sub))
        {
            throw new InvalidOperationException("Google userinfo did not include a subject.");
        }

        if (string.IsNullOrWhiteSpace(profile.Email) || profile.EmailVerified is false)
        {
            throw new InvalidOperationException("Google did not return a verified email address.");
        }

        var (firstName, lastName) = SplitName(profile.GivenName, profile.FamilyName, profile.Name);

        return new ExternalUserInfo
        {
            Provider = ProviderName,
            Subject = profile.Sub,
            Email = profile.Email.Trim(),
            FirstName = firstName,
            LastName = lastName,
            DisplayName = profile.Name,
            AvatarUrl = profile.Picture,
        };
    }

    private static (string FirstName, string LastName) SplitName(
        string? given,
        string? family,
        string? full)
    {
        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
        {
            return (
                (given ?? string.Empty).Trim(),
                (family ?? string.Empty).Trim());
        }

        var parts = (full ?? string.Empty)
            .Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => ("User", string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1]),
        };
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class GoogleUserInfo
    {
        public string? Sub { get; set; }
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public bool? EmailVerified { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        public string? Picture { get; set; }
    }
}
