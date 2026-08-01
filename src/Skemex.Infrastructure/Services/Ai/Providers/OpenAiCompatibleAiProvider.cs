using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;

namespace Skemex.Infrastructure.Services.Ai.Providers;

/// <summary>
/// OpenAI-compatible provider driven by DB settings (BaseUrl + ApiKey + AuthConfig).
/// </summary>
public sealed class OpenAiCompatibleAiProvider(
    string key,
    string displayName,
    string baseUrl,
    string? apiKey,
    AiProviderAuthConfig authConfig,
    IHttpClientFactory httpClientFactory,
    ILogger logger) : IAiProvider
{
    public const string HttpClientName = "ai-provider:openai-compatible";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name { get; } = key;

    public string DisplayName { get; } = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim();
    private string BaseUrl { get; } = baseUrl.TrimEnd('/');
    private string? ApiKey { get; } = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
    private AiProviderAuthConfig AuthConfig { get; } = authConfig;

    public async Task<IReadOnlyList<RemoteAiModel>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = CreateRequest(HttpMethod.Get, "models");
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "AI provider {Provider} models list failed with {Status}: {Body}",
                Name,
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content
            .ReadFromJsonAsync<OpenAiModelsResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return (payload?.Data ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Where(item => item.Active != false)
            .Select(item => new
            {
                Id = item.Id.Trim(),
                Author = ResolveAuthor(item.OwnedBy),
            })
            .Where(item => IsChatCompletionModel(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => new RemoteAiModel(
                ExternalId: item.Id,
                DisplayName: FormatDisplayName(item.Id),
                Author: item.Author,
                IconKey: null))
            .OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        EnsureConfigured();

        var modelId = request.Model.Trim();
        using var httpRequest = CreateRequest(HttpMethod.Post, "chat/completions");
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(
                new
                {
                    model = modelId,
                    messages = new[]
                    {
                        new { role = "system", content = request.SystemPrompt },
                        new { role = "user", content = request.UserPrompt },
                    },
                }),
            Encoding.UTF8,
            "application/json");

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "AI provider {Provider} chat completion failed with {Status}: {Body}",
                Name,
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content
            .ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        var text = payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        return new AiChatResult { Text = text };
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var queryParts = new List<string>();
        foreach (var entry in AuthConfig.Entries)
        {
            if (!string.Equals(entry.Type, AiProviderAuthEntryTypes.Query, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrEmpty(entry.Value))
            {
                continue;
            }

            queryParts.Add(
                $"{Uri.EscapeDataString(entry.Name.Trim())}={Uri.EscapeDataString(entry.Value)}");
        }

        var path = queryParts.Count == 0
            ? relativePath
            : $"{relativePath}?{string.Join('&', queryParts)}";

        var request = new HttpRequestMessage(method, new Uri($"{BaseUrl}/{path}"));

        var hasAuthorizationHeader = false;
        foreach (var entry in AuthConfig.Entries)
        {
            if (!string.Equals(entry.Type, AiProviderAuthEntryTypes.Header, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrEmpty(entry.Value))
            {
                continue;
            }

            var headerName = entry.Name.Trim();
            if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                hasAuthorizationHeader = true;
                request.Headers.TryAddWithoutValidation(headerName, entry.Value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(headerName, entry.Value);
            }
        }

        if (!hasAuthorizationHeader && !string.IsNullOrWhiteSpace(ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        }

        return request;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new InvalidOperationException($"AI provider '{Name}' is missing BaseUrl.");
        }

        var hasApiKey = !string.IsNullOrWhiteSpace(ApiKey);
        var hasAuthValue = AuthConfig.Entries.Any(entry =>
            !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrEmpty(entry.Value));

        if (!hasApiKey && !hasAuthValue)
        {
            throw new InvalidOperationException(
                $"AI provider '{Name}' requires an API key or at least one auth entry with a value.");
        }
    }

    private string ResolveAuthor(string? ownedBy)
    {
        if (!string.IsNullOrWhiteSpace(ownedBy)
            && !string.Equals(ownedBy, "system", StringComparison.OrdinalIgnoreCase))
        {
            return ownedBy.Trim();
        }

        return string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName.Trim();
    }

    /// <summary>
    /// Keep chat-completion models only (drop video/image/audio/embedding/…).
    /// </summary>
    internal static bool IsChatCompletionModel(string modelId)
    {
        var id = StripCatalogPrefixes(modelId).ToLowerInvariant();

        if (id.Contains("whisper", StringComparison.Ordinal)
            || id.Contains("tts", StringComparison.Ordinal)
            || id.Contains("dall-e", StringComparison.Ordinal)
            || id.Contains("dalle", StringComparison.Ordinal)
            || id.Contains("imagen", StringComparison.Ordinal)
            || id.Contains("veo", StringComparison.Ordinal)
            || id.Contains("lyria", StringComparison.Ordinal)
            || id.Contains("chirp", StringComparison.Ordinal)
            || id.Contains("video", StringComparison.Ordinal)
            || id.Contains("embedding", StringComparison.Ordinal)
            || id.Contains("moderation", StringComparison.Ordinal)
            || id.Contains("transcribe", StringComparison.Ordinal)
            || id.Contains("realtime", StringComparison.Ordinal)
            || id.Contains("audio", StringComparison.Ordinal)
            || id.Contains("image", StringComparison.Ordinal)
            || id.Contains("aqa", StringComparison.Ordinal)
            || id.Contains("search", StringComparison.Ordinal)
            || id.Contains("playai", StringComparison.Ordinal)
            || id.Contains("orpheus", StringComparison.Ordinal)
            || id.Contains("guard", StringComparison.Ordinal)
            || id.Contains("safeguard", StringComparison.Ordinal)
            || id.Contains("robotics", StringComparison.Ordinal)
            || id.Contains("computer-use", StringComparison.Ordinal)
            || id.StartsWith("text-", StringComparison.Ordinal)
            || id.StartsWith("davinci", StringComparison.Ordinal)
            || id.StartsWith("babbage", StringComparison.Ordinal)
            || id.StartsWith("curie", StringComparison.Ordinal)
            || id.StartsWith("ada", StringComparison.Ordinal))
        {
            return false;
        }

        if (id.StartsWith("gpt-", StringComparison.Ordinal)
            || id.StartsWith("o1", StringComparison.Ordinal)
            || id.StartsWith("o3", StringComparison.Ordinal)
            || id.StartsWith("o4", StringComparison.Ordinal)
            || id.StartsWith("chatgpt", StringComparison.Ordinal)
            || id.StartsWith("ft:gpt-", StringComparison.Ordinal)
            || id.Contains("gemini", StringComparison.Ordinal)
            || id.Contains("gemma", StringComparison.Ordinal)
            || id.Contains("llama", StringComparison.Ordinal)
            || id.Contains("qwen", StringComparison.Ordinal)
            || id.Contains("mistral", StringComparison.Ordinal)
            || id.Contains("mixtral", StringComparison.Ordinal)
            || id.Contains("deepseek", StringComparison.Ordinal)
            || id.Contains("claude", StringComparison.Ordinal)
            || id.Contains("command", StringComparison.Ordinal)
            || id.Contains("phi-", StringComparison.Ordinal)
            || id.Contains("moonshot", StringComparison.Ordinal)
            || id.Contains("kimi", StringComparison.Ordinal)
            || id.Contains("compound", StringComparison.Ordinal)
            || id.Contains("allam", StringComparison.Ordinal)
            || id.Contains("gpt-oss", StringComparison.Ordinal))
        {
            return true;
        }

        // Unknown IDs from broad catalogs (e.g. Gemini): deny by default.
        // Groq-style short chat slugs with no specialty markers are allowed above via families.
        return false;
    }

    /// <summary>
    /// Human-readable label: strip <c>models/</c> and author prefixes like <c>openai/</c> or <c>groq/</c>,
    /// then title-case the remainder (same idea as the old AppSettings providers).
    /// </summary>
    internal static string FormatDisplayName(string externalId)
    {
        var raw = StripCatalogPrefixes(externalId);
        if (raw.Length == 0)
        {
            return externalId;
        }

        if (raw.StartsWith("ft:", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[3..];
        }

        raw = raw.Replace('-', ' ').Replace('_', ' ').Replace(':', ' ').Trim();
        if (raw.Length == 0)
        {
            return externalId;
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 1)
            {
                parts[i] = part.ToUpperInvariant();
                continue;
            }

            if (part.Any(char.IsDigit) && part.All(c => char.IsDigit(c) || c == '.'))
            {
                parts[i] = part;
                continue;
            }

            if (string.Equals(part, "gpt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "o1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "o3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "o4", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "oss", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = part.ToUpperInvariant();
                continue;
            }

            parts[i] = char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Removes <c>models/</c> and a single author/org segment (<c>openai/…</c>, <c>groq/…</c>).
    /// </summary>
    internal static string StripCatalogPrefixes(string externalId)
    {
        var raw = externalId.Trim();
        while (raw.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw["models/".Length..];
        }

        var slash = raw.IndexOf('/');
        if (slash > 0 && slash < raw.Length - 1)
        {
            // author/model → model (openai/gpt-oss-120b, groq/compound)
            raw = raw[(slash + 1)..];
        }

        return raw.Trim();
    }

    private sealed class OpenAiModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAiModelItem> Data { get; set; } = [];
    }

    private sealed class OpenAiModelItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("owned_by")]
        public string? OwnedBy { get; set; }

        [JsonPropertyName("active")]
        public bool? Active { get; set; }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessagePayload? Message { get; set; }
    }

    private sealed class ChatMessagePayload
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
