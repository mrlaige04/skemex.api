using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;

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

    public async Task<JiraUserDto?> FindUserByEmailAsync(
        string accessToken,
        string cloudId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var trimmed = email.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var users = await SearchUsersAsync(accessToken, cloudId, trimmed, cancellationToken);
        var match = users.FirstOrDefault(u =>
            !string.IsNullOrWhiteSpace(u.AccountId)
            && string.Equals(u.EmailAddress, trimmed, StringComparison.OrdinalIgnoreCase));

        return ToUserDto(match);
    }

    public async Task<JiraUserDto?> FindUserByDisplayNameAsync(
        string accessToken,
        string cloudId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var trimmed = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        var users = await SearchUsersAsync(accessToken, cloudId, trimmed, cancellationToken);
        var match = users.FirstOrDefault(u =>
            !string.IsNullOrWhiteSpace(u.AccountId)
            && string.Equals(u.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase));

        return ToUserDto(match);
    }

    private async Task<List<UserPayload>> SearchUsersAsync(
        string accessToken,
        string cloudId,
        string query,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var url =
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/user/search"
            + $"?query={Uri.EscapeDataString(query)}&maxResults=20";

        using var request = CreateAuthorizedGet(url, accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira user search failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        return JsonSerializer.Deserialize<List<UserPayload>>(body, JsonOptions) ?? [];
    }

    private static JiraUserDto? ToUserDto(UserPayload? match)
    {
        if (match is null || string.IsNullOrWhiteSpace(match.AccountId))
        {
            return null;
        }

        return new JiraUserDto
        {
            AccountId = match.AccountId,
            DisplayName = match.DisplayName,
            EmailAddress = match.EmailAddress,
            Active = match.Active ?? true,
            AvatarUrl = PickAvatarUrl(match.AvatarUrls),
        };
    }

    public async Task<IReadOnlyList<JiraUserDto>> GetAssignableUsersAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await FetchAssignableUsersPageAsync(
                accessToken,
                cloudId,
                projectKey,
                maxResults,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("401", StringComparison.Ordinal)
                                                   || ex.Message.Contains("scope does not match", StringComparison.OrdinalIgnoreCase))
        {
            // Older tokens / console apps may lack read:jira-user for assignable search.
            // Fall back to unique assignees visible via issue search (read:jira-work).
            return await FetchUsersFromProjectIssuesAsync(
                accessToken,
                cloudId,
                projectKey,
                maxResults,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<JiraUserDto>> FetchAssignableUsersPageAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var collected = new List<JiraUserDto>();
        var startAt = 0;
        const int pageSize = 50;
        var cap = Math.Clamp(maxResults, 1, 500);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        while (collected.Count < cap)
        {
            var take = Math.Min(pageSize, cap - collected.Count);
            // OAuth classic scopes map cleanly to api/3; api/2 often returns "scope does not match".
            var url =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/user/assignable/search"
                + $"?project={Uri.EscapeDataString(projectKey)}"
                + $"&maxResults={take}&startAt={startAt}";

            using var request = CreateAuthorizedGet(url, accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Jira assignable users failed ({(int)response.StatusCode}): {Truncate(body)}");
            }

            var page = JsonSerializer.Deserialize<List<UserPayload>>(body, JsonOptions) ?? [];
            if (page.Count == 0)
            {
                break;
            }

            foreach (var user in page)
            {
                var dto = ToUserDto(user);
                if (dto is null || !seen.Add(dto.AccountId))
                {
                    continue;
                }

                collected.Add(dto);
                if (collected.Count >= cap)
                {
                    break;
                }
            }

            startAt += page.Count;
            if (page.Count < take)
            {
                break;
            }
        }

        return collected
            .OrderBy(u => u.DisplayName ?? u.EmailAddress ?? u.AccountId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<JiraUserDto>> FetchUsersFromProjectIssuesAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var issues = await SearchProjectIssuesAsync(
            accessToken,
            cloudId,
            projectKey,
            Math.Clamp(maxResults, 1, 500),
            cancellationToken);

        var byKey = new Dictionary<string, JiraUserDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in issues)
        {
            var name = issue.AssigneeDisplayName?.Trim();
            var email = issue.AssigneeEmail?.Trim();
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var key = !string.IsNullOrWhiteSpace(email) ? email! : name!;
            if (byKey.ContainsKey(key))
            {
                continue;
            }

            byKey[key] = new JiraUserDto
            {
                AccountId = key,
                DisplayName = name,
                EmailAddress = email,
                Active = true,
            };
        }

        return byKey.Values
            .OrderBy(u => u.DisplayName ?? u.EmailAddress ?? u.AccountId, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxResults, 1, 500))
            .ToList();
    }

    private static string? PickAvatarUrl(Dictionary<string, string>? avatarUrls)
    {
        if (avatarUrls is null || avatarUrls.Count == 0)
        {
            return null;
        }

        foreach (var key in new[] { "48x48", "32x32", "24x24", "16x16" })
        {
            if (avatarUrls.TryGetValue(key, out var url) && !string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return avatarUrls.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    public async Task<IReadOnlyList<JiraIssueTypeDto>> GetProjectIssueTypesAsync(
        string accessToken,
        string cloudId,
        string projectKeyOrId,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var collected = new List<JiraIssueTypeDto>();
        var startAt = 0;
        const int pageSize = 50;

        while (true)
        {
            var url =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue/createmeta/"
                + $"{Uri.EscapeDataString(projectKeyOrId)}/issuetypes"
                + $"?startAt={startAt}&maxResults={pageSize}";

            using var request = CreateAuthorizedGet(url, accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Jira issue types failed ({(int)response.StatusCode}): {Truncate(body)}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("issueTypes", out var typesEl)
                || typesEl.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var pageCount = 0;
            foreach (var typeEl in typesEl.EnumerateArray())
            {
                pageCount++;
                var id = typeEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var name = typeEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var subtask = typeEl.TryGetProperty("subtask", out var subEl)
                              && subEl.ValueKind == JsonValueKind.True;
                collected.Add(new JiraIssueTypeDto
                {
                    Id = id,
                    Name = name,
                    Subtask = subtask,
                });
            }

            var total = root.TryGetProperty("total", out var totalEl) && totalEl.TryGetInt32(out var t)
                ? t
                : startAt + pageCount;
            startAt += pageCount;
            if (pageCount == 0 || startAt >= total)
            {
                break;
            }
        }

        return collected;
    }

    public async Task<JiraCreatedIssueDto> CreateIssueAsync(
        string accessToken,
        string cloudId,
        JiraCreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        object issueType = !string.IsNullOrWhiteSpace(request.IssueTypeId)
            ? new { id = request.IssueTypeId }
            : new { name = request.IssueTypeName };

        var fields = new Dictionary<string, object?>
        {
            ["project"] = new { key = request.ProjectKey },
            ["summary"] = request.Summary,
            ["issuetype"] = issueType,
        };

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            fields["description"] = request.Description;
        }

        if (!string.IsNullOrWhiteSpace(request.AssigneeAccountId))
        {
            fields["assignee"] = new { accountId = request.AssigneeAccountId };
        }

        // Parent by key (Jira docs): { "parent": { "key": "PROJ-123" } }.
        // Id is only a fallback when key is unavailable.
        if (!string.IsNullOrWhiteSpace(request.ParentIssueKey))
        {
            fields["parent"] = new { key = request.ParentIssueKey };
        }
        else if (!string.IsNullOrWhiteSpace(request.ParentIssueId))
        {
            fields["parent"] = new { id = request.ParentIssueId };
        }

        if (request.Labels.Count > 0)
        {
            fields["labels"] = request.Labels
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(SanitizeLabel)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.OriginalEstimate)
            || !string.IsNullOrWhiteSpace(request.RemainingEstimate))
        {
            fields["timetracking"] = new
            {
                originalEstimate = request.OriginalEstimate,
                remainingEstimate = request.RemainingEstimate,
            };
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue")
        {
            Content = JsonContent.Create(new { fields }, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira create issue failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        var created = JsonSerializer.Deserialize<CreatedIssuePayload>(body, JsonOptions)
                      ?? throw new InvalidOperationException("Jira create issue returned empty body.");

        if (string.IsNullOrWhiteSpace(created.Id) || string.IsNullOrWhiteSpace(created.Key))
        {
            throw new InvalidOperationException("Jira create issue response missing id/key.");
        }

        return new JiraCreatedIssueDto { Id = created.Id, Key = created.Key };
    }

    public async Task<JiraIssueRefDto?> TryGetIssueAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueIdOrKey))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var url =
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue/"
            + $"{Uri.EscapeDataString(issueIdOrKey.Trim())}"
            + "?fields=issuetype,summary";
        using var request = CreateAuthorizedGet(url, accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira get issue failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var key = root.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var typeName = "Task";
        var subtask = false;
        if (root.TryGetProperty("fields", out var fields)
            && fields.TryGetProperty("issuetype", out var typeEl))
        {
            if (typeEl.TryGetProperty("name", out var nameEl)
                && nameEl.GetString() is { Length: > 0 } name)
            {
                typeName = name;
            }

            subtask = typeEl.TryGetProperty("subtask", out var subEl)
                      && subEl.ValueKind == JsonValueKind.True;
        }

        return new JiraIssueRefDto
        {
            Id = id,
            Key = key,
            IssueTypeName = typeName,
            Subtask = subtask,
        };
    }

    public async Task<JiraRemoteIssueDto?> GetRemoteIssueAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueIdOrKey))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var fields = string.Join(
            ',',
            "summary",
            "description",
            "issuetype",
            "status",
            "assignee",
            "parent",
            "labels",
            "timetracking",
            "worklog");
        var url =
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/issue/"
            + $"{Uri.EscapeDataString(issueIdOrKey.Trim())}"
            + $"?fields={Uri.EscapeDataString(fields)}";

        using var request = CreateAuthorizedGet(url, accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira get issue failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        using var document = JsonDocument.Parse(body);
        return MapRemoteIssue(document.RootElement);
    }

    public async Task<JiraRegisteredWebhookDto> RegisterWebhookAsync(
        string accessToken,
        string cloudId,
        JiraRegisterWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("Webhook URL is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ProjectKey))
        {
            throw new ArgumentException("Project key is required.", nameof(request));
        }

        // OAuth 3LO must use dynamic webhooks (api/3), not admin /rest/webhooks/1.0.
        var events = request.Events.Count > 0
            ? request.Events
            : JiraWebhookEventCatalog.OAuthDynamicJiraEventNames;

        var jql = $"project = \"{EscapeJqlValue(request.ProjectKey.Trim())}\"";
        var payload = new Dictionary<string, object?>
        {
            ["url"] = request.Url.Trim(),
            ["webhooks"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["jqlFilter"] = jql,
                    ["events"] = events,
                },
            },
        };

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/webhook")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira register webhook failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        var id = TryExtractDynamicWebhookId(body);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                $"Jira register webhook response missing id: {Truncate(body)}");
        }

        return new JiraRegisteredWebhookDto { Id = id };
    }

    public async Task DeleteWebhookAsync(
        string accessToken,
        string cloudId,
        string webhookId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookId))
        {
            return;
        }

        if (!long.TryParse(webhookId.Trim(), out var numericId))
        {
            return;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/webhook")
        {
            Content = JsonContent.Create(
                new Dictionary<string, object?> { ["webhookIds"] = new[] { numericId } },
                options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode
            || response.StatusCode == System.Net.HttpStatusCode.NotFound
            || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Jira delete webhook failed ({(int)response.StatusCode}): {Truncate(body)}");
    }

    public async Task RefreshWebhookAsync(
        string accessToken,
        string cloudId,
        string webhookId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookId) || !long.TryParse(webhookId.Trim(), out var numericId))
        {
            return;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/webhook/refresh")
        {
            Content = JsonContent.Create(
                new Dictionary<string, object?> { ["webhookIds"] = new[] { numericId } },
                options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Jira refresh webhook failed ({(int)response.StatusCode}): {Truncate(body)}");
    }

    public async Task<(Stream Content, string ContentType, string? FileName)?> DownloadAttachmentAsync(
        string accessToken,
        string cloudId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var metaRequest = CreateAuthorizedGet(
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/attachment/{Uri.EscapeDataString(attachmentId.Trim())}",
            accessToken);
        using var metaResponse = await client.SendAsync(metaRequest, cancellationToken);
        if (!metaResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var metaJson = await metaResponse.Content.ReadAsStringAsync(cancellationToken);
        string? fileName = null;
        string? contentUrl = null;
        string contentType = "application/octet-stream";
        try
        {
            using var doc = JsonDocument.Parse(metaJson);
            var root = doc.RootElement;
            fileName = root.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
            contentUrl = root.TryGetProperty("content", out var cu) ? cu.GetString() : null;
            if (root.TryGetProperty("mimeType", out var mt) && mt.ValueKind == JsonValueKind.String)
            {
                contentType = mt.GetString() ?? contentType;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(contentUrl))
        {
            contentUrl =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/attachment/content/{Uri.EscapeDataString(attachmentId.Trim())}";
        }

        using var contentRequest = CreateAuthorizedGet(contentUrl, accessToken);
        var contentResponse = await client.SendAsync(
            contentRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!contentResponse.IsSuccessStatusCode)
        {
            contentResponse.Dispose();
            return null;
        }

        if (contentResponse.Content.Headers.ContentType?.MediaType is { } mediaType)
        {
            contentType = mediaType;
        }

        await using var stream = await contentResponse.Content.ReadAsStreamAsync(cancellationToken);
        // Buffer so we can dispose the HTTP response before returning.
        var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        contentResponse.Dispose();
        memory.Position = 0;
        return (memory, contentType, fileName);
    }

    private static string? TryExtractDynamicWebhookId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("webhookRegistrationResult", out var results)
                && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in results.EnumerateArray())
                {
                    var id = ReadCreatedWebhookId(item);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }

                    if (item.TryGetProperty("errors", out var errors)
                        && errors.ValueKind == JsonValueKind.Array
                        && errors.GetArrayLength() > 0)
                    {
                        var messages = string.Join("; ", errors.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)));
                        throw new InvalidOperationException(
                            $"Jira rejected webhook registration: {messages}");
                    }
                }
            }

            // Some responses return a bare array of registration results.
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var id = ReadCreatedWebhookId(item);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
            }

            return ReadCreatedWebhookId(root) ?? TryReadIdProperty(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadCreatedWebhookId(JsonElement item)
    {
        if (item.TryGetProperty("createdWebhookId", out var created))
        {
            return created.ValueKind switch
            {
                JsonValueKind.Number => created.GetRawText(),
                JsonValueKind.String => created.GetString(),
                _ => null,
            };
        }

        return null;
    }

    private static string? TryReadIdProperty(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idEl))
        {
            return null;
        }

        return idEl.ValueKind switch
        {
            JsonValueKind.Number => idEl.GetRawText(),
            JsonValueKind.String => idEl.GetString(),
            _ => null,
        };
    }

    public async Task UpdateIssueParentAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        string parentIssueKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parentIssueKey))
        {
            throw new ArgumentException("Parent issue key is required.", nameof(parentIssueKey));
        }

        // Same API version as create; parent by key per Jira Cloud docs.
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Put,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue/{Uri.EscapeDataString(issueIdOrKey)}")
        {
            Content = JsonContent.Create(
                new { fields = new { parent = new { key = parentIssueKey.Trim() } } },
                options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Jira update parent failed ({(int)response.StatusCode}): {Truncate(body)}");
    }

    public async Task<JiraCreatedWorklogDto> AddWorklogAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        JiraAddWorklogRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["started"] = request.Started,
            ["timeSpentSeconds"] = Math.Max(60, request.TimeSpentSeconds),
        };
        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            payload["comment"] = request.Comment;
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue/{Uri.EscapeDataString(issueIdOrKey)}/worklog")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira add worklog failed ({(int)response.StatusCode}): {Truncate(body)}");
        }

        using var document = JsonDocument.Parse(body);
        var id = document.RootElement.TryGetProperty("id", out var idEl)
            ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString())
            : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Jira add worklog response missing id.");
        }

        return new JiraCreatedWorklogDto { Id = id };
    }

    public async Task<JiraCreatedCommentDto> AddIssueCommentAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        JiraAddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Comment body is required.", nameof(request));
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue/{Uri.EscapeDataString(issueIdOrKey)}/comment")
        {
            Content = JsonContent.Create(new { body = request.Body }, options: JsonOptions),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Jira add comment failed ({(int)response.StatusCode}): {Truncate(responseBody)}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var id = document.RootElement.TryGetProperty("id", out var idEl)
            ? idEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Jira add comment response missing id.");
        }

        return new JiraCreatedCommentDto { Id = id };
    }

    public async Task<IReadOnlyList<JiraRemoteCommentDto>> GetIssueCommentsAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var collected = new List<JiraRemoteCommentDto>();
        const int pageSize = 100;
        var startAt = 0;

        while (true)
        {
            var url =
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/2/issue/{Uri.EscapeDataString(issueIdOrKey)}/comment"
                + $"?startAt={startAt}&maxResults={pageSize}";

            using var httpRequest = CreateAuthorizedGet(url, accessToken);
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Jira list comments failed ({(int)response.StatusCode}): {Truncate(body)}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("comments", out var commentsEl)
                || commentsEl.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var pageCount = 0;
            foreach (var commentEl in commentsEl.EnumerateArray())
            {
                pageCount++;
                var mapped = MapRemoteComment(commentEl);
                if (mapped is not null)
                {
                    collected.Add(mapped);
                }
            }

            var total = root.TryGetProperty("total", out var totalEl) && totalEl.TryGetInt32(out var totalValue)
                ? totalValue
                : collected.Count;
            startAt += pageCount;
            if (pageCount == 0 || startAt >= total)
            {
                break;
            }
        }

        return collected;
    }

    private static JiraRemoteCommentDto? MapRemoteComment(JsonElement commentEl)
    {
        var id = commentEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        string? body = null;
        if (commentEl.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind != JsonValueKind.Null)
        {
            body = ExtractJiraText(bodyEl);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        string? authorEmail = null;
        string? authorDisplayName = null;
        if (commentEl.TryGetProperty("author", out var authorEl)
            && authorEl.ValueKind == JsonValueKind.Object)
        {
            authorEmail = authorEl.TryGetProperty("emailAddress", out var emailEl)
                ? emailEl.GetString()
                : null;
            authorDisplayName = authorEl.TryGetProperty("displayName", out var nameEl)
                ? nameEl.GetString()
                : null;
        }

        DateTime? createdAt = null;
        if (commentEl.TryGetProperty("created", out var createdEl))
        {
            createdAt = ParseJiraDateTime(createdEl.GetString());
        }

        return new JiraRemoteCommentDto
        {
            Id = id,
            Body = body.Trim(),
            AuthorEmail = authorEmail,
            AuthorDisplayName = authorDisplayName,
            CreatedAt = createdAt,
        };
    }

    public async Task<IReadOnlyList<JiraRemoteIssueDto>> SearchProjectIssuesAsync(
        string accessToken,
        string cloudId,
        string projectKey,
        int maxResults = 200,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var collected = new List<JiraRemoteIssueDto>();
        const int pageSize = 50;
        var cap = Math.Clamp(maxResults, 1, 500);
        var jql = $"project = \"{EscapeJqlValue(projectKey)}\" ORDER BY created ASC";
        string? nextPageToken = null;
        var fields = new[]
        {
            "summary",
            "description",
            "issuetype",
            "status",
            "assignee",
            "parent",
            "labels",
            "timetracking",
            "worklog",
        };

        while (collected.Count < cap)
        {
            var take = Math.Min(pageSize, cap - collected.Count);
            object payload = string.IsNullOrEmpty(nextPageToken)
                ? new { jql, maxResults = take, fields }
                : new { jql, maxResults = take, fields, nextPageToken };

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.atlassian.com/ex/jira/{cloudId}/rest/api/3/search/jql")
            {
                Content = JsonContent.Create(payload, options: JsonOptions),
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Jira issue search failed ({(int)response.StatusCode}): {Truncate(body)}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("issues", out var issuesEl) || issuesEl.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var pageCount = 0;
            foreach (var issueEl in issuesEl.EnumerateArray())
            {
                pageCount++;
                var mapped = MapRemoteIssue(issueEl);
                if (mapped is not null)
                {
                    collected.Add(mapped);
                }
            }

            var isLast = root.TryGetProperty("isLast", out var isLastEl)
                && isLastEl.ValueKind == JsonValueKind.True;
            nextPageToken = root.TryGetProperty("nextPageToken", out var tokenEl)
                ? tokenEl.GetString()
                : null;

            if (pageCount == 0 || isLast || string.IsNullOrEmpty(nextPageToken) || collected.Count >= cap)
            {
                break;
            }
        }

        return collected;
    }

    private static JiraRemoteIssueDto? MapRemoteIssue(JsonElement issueEl)
    {
        var id = issueEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var key = issueEl.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!issueEl.TryGetProperty("fields", out var fields))
        {
            return new JiraRemoteIssueDto { Id = id, Key = key };
        }

        var summary = fields.TryGetProperty("summary", out var summaryEl)
            ? summaryEl.GetString() ?? string.Empty
            : string.Empty;

        string? description = null;
        if (fields.TryGetProperty("description", out var descEl) && descEl.ValueKind != JsonValueKind.Null)
        {
            description = ExtractJiraText(descEl);
        }

        var issueType = "Task";
        if (fields.TryGetProperty("issuetype", out var typeEl)
            && typeEl.TryGetProperty("name", out var typeNameEl))
        {
            issueType = typeNameEl.GetString() ?? "Task";
        }

        string? status = null;
        if (fields.TryGetProperty("status", out var statusEl)
            && statusEl.TryGetProperty("name", out var statusNameEl))
        {
            status = statusNameEl.GetString();
        }

        string? assigneeAccountId = null;
        string? assigneeEmail = null;
        string? assigneeDisplayName = null;
        if (fields.TryGetProperty("assignee", out var assigneeEl)
            && assigneeEl.ValueKind == JsonValueKind.Object)
        {
            assigneeAccountId = assigneeEl.TryGetProperty("accountId", out var accountIdEl)
                ? accountIdEl.GetString()
                : null;
            assigneeEmail = assigneeEl.TryGetProperty("emailAddress", out var emailEl)
                ? emailEl.GetString()
                : null;
            assigneeDisplayName = assigneeEl.TryGetProperty("displayName", out var nameEl)
                ? nameEl.GetString()
                : null;
        }

        string? parentKey = null;
        if (fields.TryGetProperty("parent", out var parentEl)
            && parentEl.ValueKind == JsonValueKind.Object
            && parentEl.TryGetProperty("key", out var parentKeyEl))
        {
            parentKey = parentKeyEl.GetString();
        }

        var labels = new List<string>();
        if (fields.TryGetProperty("labels", out var labelsEl) && labelsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var label in labelsEl.EnumerateArray())
            {
                var value = label.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    labels.Add(value);
                }
            }
        }

        int? originalMinutes = null;
        int? remainingMinutes = null;
        int? spentMinutes = null;
        if (fields.TryGetProperty("timetracking", out var timeEl) && timeEl.ValueKind == JsonValueKind.Object)
        {
            if (timeEl.TryGetProperty("originalEstimateSeconds", out var oe)
                && oe.TryGetInt32(out var oeSec))
            {
                originalMinutes = Math.Max(0, oeSec / 60);
            }

            if (timeEl.TryGetProperty("remainingEstimateSeconds", out var re)
                && re.TryGetInt32(out var reSec))
            {
                remainingMinutes = Math.Max(0, reSec / 60);
            }

            if (timeEl.TryGetProperty("timeSpentSeconds", out var ts)
                && ts.TryGetInt32(out var tsSec))
            {
                spentMinutes = Math.Max(0, tsSec / 60);
            }
        }

        var worklogs = new List<JiraRemoteWorklogDto>();
        if (fields.TryGetProperty("worklog", out var worklogEl)
            && worklogEl.ValueKind == JsonValueKind.Object
            && worklogEl.TryGetProperty("worklogs", out var worklogsEl)
            && worklogsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var wl in worklogsEl.EnumerateArray())
            {
                var worklogId = wl.TryGetProperty("id", out var wlIdEl)
                    ? (wlIdEl.ValueKind == JsonValueKind.Number ? wlIdEl.GetRawText() : wlIdEl.GetString())
                    : null;
                var started = wl.TryGetProperty("started", out var startedEl)
                    ? ParseJiraDateTime(startedEl.GetString())
                    : null;
                var seconds = wl.TryGetProperty("timeSpentSeconds", out var secEl) && secEl.TryGetInt32(out var s)
                    ? s
                    : 0;
                if (started is null || seconds <= 0)
                {
                    continue;
                }

                string? comment = null;
                if (wl.TryGetProperty("comment", out var commentEl) && commentEl.ValueKind != JsonValueKind.Null)
                {
                    comment = ExtractJiraText(commentEl);
                }

                string? authorEmail = null;
                if (wl.TryGetProperty("author", out var authorEl)
                    && authorEl.TryGetProperty("emailAddress", out var authorEmailEl))
                {
                    authorEmail = authorEmailEl.GetString();
                }

                worklogs.Add(new JiraRemoteWorklogDto
                {
                    Id = worklogId,
                    StartedAt = started.Value,
                    SpentMinutes = Math.Max(1, seconds / 60),
                    Comment = comment,
                    AuthorEmail = authorEmail,
                });
            }
        }

        return new JiraRemoteIssueDto
        {
            Id = id,
            Key = key,
            Summary = summary,
            Description = description,
            IssueType = issueType,
            Status = status,
            AssigneeAccountId = assigneeAccountId,
            AssigneeEmail = assigneeEmail,
            AssigneeDisplayName = assigneeDisplayName,
            ParentKey = parentKey,
            Labels = labels,
            OriginalEstimateMinutes = originalMinutes,
            RemainingEstimateMinutes = remainingMinutes,
            TimeSpentMinutes = spentMinutes,
            Worklogs = worklogs,
        };
    }

    private static DateTime? ParseJiraDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var dto))
        {
            return dto.UtcDateTime;
        }

        return null;
    }

    /// <summary>
    /// Extracts plain text from Jira Cloud API v3 fields (ADF documents or plain strings).
    /// </summary>
    private static string? ExtractJiraText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parts = new List<string>();
        CollectAdfText(element, parts);
        if (parts.Count == 0)
        {
            return null;
        }

        var text = string.Join(string.Empty, parts).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void CollectAdfText(JsonElement element, List<string> parts)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            parts.Add(element.GetString() ?? string.Empty);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("type", out var typeEl)
            && typeEl.GetString() is { } nodeType
            && (nodeType is "paragraph" or "heading" or "bulletList" or "orderedList" or "listItem"
                or "blockquote" or "codeBlock" or "rule")
            && parts.Count > 0
            && parts[^1] is not ("\n" or "\n\n"))
        {
            parts.Add("\n");
        }

        if (element.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
        {
            parts.Add(textEl.GetString() ?? string.Empty);
        }

        if (element.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in contentEl.EnumerateArray())
            {
                CollectAdfText(child, parts);
            }
        }
    }

    private static string EscapeJqlValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static HttpRequestMessage CreateAuthorizedGet(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string SanitizeLabel(string label)
    {
        var cleaned = new string(label.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray());
        return cleaned.Length <= 255 ? cleaned : cleaned[..255];
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

    private sealed class UserPayload
    {
        public string? AccountId { get; set; }
        public string? DisplayName { get; set; }
        public string? EmailAddress { get; set; }
        public bool? Active { get; set; }
        public Dictionary<string, string>? AvatarUrls { get; set; }
    }

    private sealed class CreatedIssuePayload
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
    }
}
