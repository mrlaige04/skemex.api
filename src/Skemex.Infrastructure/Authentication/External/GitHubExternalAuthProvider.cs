using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Skemex.Infrastructure.Authentication.External;

public sealed class GitHubExternalAuthProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ExternalAuthOptions> options) : IExternalAuthProvider
{
    public const string ProviderName = "github";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name => ProviderName;
    public string DisplayName => "GitHub";

    public bool IsConfigured => options.Value.GitHub.IsConfigured;

    public Uri BuildAuthorizationUrl(string redirectUri, string state)
    {
        var clientId = options.Value.GitHub.ClientId
            ?? throw new InvalidOperationException("GitHub OAuth ClientId is not configured.");

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "read:user user:email",
            ["state"] = state,
            ["allow_signup"] = "true",
        };

        return new UriBuilder("https://github.com/login/oauth/authorize")
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
        var github = options.Value.GitHub;
        var client = httpClientFactory.CreateClient(nameof(GitHubExternalAuthProvider));

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = github.ClientId!,
            ["client_secret"] = github.ClientSecret!,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
        });

        using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub token exchange failed: {tokenJson}");
        }

        var token = JsonSerializer.Deserialize<GitHubTokenResponse>(tokenJson, JsonOptions)
            ?? throw new InvalidOperationException("GitHub token response was empty.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(token.ErrorDescription)
                    ? "GitHub did not return an access token."
                    : token.ErrorDescription);
        }

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        userRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        userRequest.Headers.UserAgent.ParseAdd("Skemex");

        using var userResponse = await client.SendAsync(userRequest, cancellationToken).ConfigureAwait(false);
        var userJson = await userResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!userResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GitHub user profile failed: {userJson}");
        }

        var profile = JsonSerializer.Deserialize<GitHubUser>(userJson, JsonOptions)
            ?? throw new InvalidOperationException("GitHub user profile was empty.");

        if (profile.Id is null or 0)
        {
            throw new InvalidOperationException("GitHub user profile did not include an id.");
        }

        var email = await ResolveEmailAsync(client, token.AccessToken, profile.Email, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                "GitHub did not return a verified email. Make an email public or grant the user:email scope.");
        }

        var (firstName, lastName) = SplitName(profile.Name, profile.Login);

        return new ExternalUserInfo
        {
            Provider = ProviderName,
            Subject = profile.Id.Value.ToString(),
            Email = email.Trim(),
            FirstName = firstName,
            LastName = lastName,
            DisplayName = profile.Name ?? profile.Login,
            AvatarUrl = profile.AvatarUrl,
        };
    }

    private static async Task<string?> ResolveEmailAsync(
        HttpClient client,
        string accessToken,
        string? profileEmail,
        CancellationToken cancellationToken)
    {
        using var emailsRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        emailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        emailsRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        emailsRequest.Headers.UserAgent.ParseAdd("Skemex");

        using var emailsResponse = await client.SendAsync(emailsRequest, cancellationToken).ConfigureAwait(false);
        if (emailsResponse.IsSuccessStatusCode)
        {
            var emailsJson = await emailsResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(emailsJson, JsonOptions) ?? [];
            var primaryVerified = emails.FirstOrDefault(entry => entry.Primary && entry.Verified);
            if (!string.IsNullOrWhiteSpace(primaryVerified?.Email))
            {
                return primaryVerified.Email;
            }

            var anyVerified = emails.FirstOrDefault(entry => entry.Verified);
            if (!string.IsNullOrWhiteSpace(anyVerified?.Email))
            {
                return anyVerified.Email;
            }
        }

        return string.IsNullOrWhiteSpace(profileEmail) ? null : profileEmail;
    }

    private static (string FirstName, string LastName) SplitName(string? fullName, string? login)
    {
        var parts = (fullName ?? string.Empty)
            .Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            0 => (string.IsNullOrWhiteSpace(login) ? "User" : login.Trim(), string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1]),
        };
    }

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private sealed class GitHubUser
    {
        public long? Id { get; set; }
        public string? Login { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }
    }

    private sealed class GitHubEmail
    {
        public string? Email { get; set; }
        public bool Primary { get; set; }
        public bool Verified { get; set; }
    }
}
