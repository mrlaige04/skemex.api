using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed class JiraApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<JiraOptions> jiraOptions) : IJiraApiClient
{
    public const string HttpClientName = "JiraAtlassian";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly JiraOptions _options = jiraOptions.Value;

    public async Task<JiraTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(
            "https://auth.atlassian.com/oauth/token",
            new
            {
                grant_type = "authorization_code",
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                code,
                redirect_uri = redirectUri,
            },
            cancellationToken);

        return await ReadTokenAsync(response, cancellationToken);
    }

    public async Task<JiraTokenResponse> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(
            "https://auth.atlassian.com/oauth/token",
            new
            {
                grant_type = "refresh_token",
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                refresh_token = refreshToken,
            },
            cancellationToken);

        return await ReadTokenAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<JiraAccessibleResourceDto>> GetAccessibleResourcesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.atlassian.com/oauth/token/accessible-resources");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<AccessibleResourcePayload>>(
            JsonOptions,
            cancellationToken);

        return (payload ?? [])
            .Select(r => new JiraAccessibleResourceDto
            {
                Id = r.Id ?? string.Empty,
                Url = r.Url ?? string.Empty,
                Name = r.Name ?? string.Empty,
                Scopes = r.Scopes ?? [],
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Id))
            .ToList();
    }

    public async Task<IReadOnlyList<JiraProjectDto>> GetProjectsAsync(
        string accessToken,
        string cloudId,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/project");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<ProjectPayload>>(
            JsonOptions,
            cancellationToken);

        return (payload ?? [])
            .Select(p => new JiraProjectDto
            {
                Id = p.Id ?? string.Empty,
                Key = p.Key ?? string.Empty,
                Name = p.Name ?? string.Empty,
                ProjectTypeKey = p.ProjectTypeKey,
            })
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<JiraTokenResponse> ReadTokenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Atlassian token endpoint returned {(int)response.StatusCode}: {Truncate(body)}");
        }

        var payload = JsonSerializer.Deserialize<TokenPayload>(body, JsonOptions)
                      ?? throw new InvalidOperationException("Atlassian token response was empty.");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new InvalidOperationException("Atlassian token response missing access_token.");
        }

        return new JiraTokenResponse
        {
            AccessToken = payload.AccessToken,
            RefreshToken = payload.RefreshToken ?? string.Empty,
            ExpiresIn = payload.ExpiresIn,
            Scope = payload.Scope,
            TokenType = payload.TokenType ?? "Bearer",
        };
    }

    private static string Truncate(string value) =>
        value.Length <= 400 ? value : value[..400] + "…";

    private sealed class TokenPayload
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private sealed class AccessibleResourcePayload
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
        public List<string>? Scopes { get; set; }
    }

    private sealed class ProjectPayload
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public string? Name { get; set; }
        public string? ProjectTypeKey { get; set; }
    }
}
