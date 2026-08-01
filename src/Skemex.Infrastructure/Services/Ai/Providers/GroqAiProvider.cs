using System.ClientModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;

namespace Skemex.Infrastructure.Services.Ai.Providers;

/// <summary>
/// Groq models API — OpenAI-compatible chat via Microsoft.Extensions.AI,
/// and <c>GET /models</c> for the catalog.
/// </summary>
public sealed class GroqAiProvider(
    IOptionsMonitor<AiOptions> aiOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<GroqAiProvider> logger) : IAiProvider
{
    public const string HttpClientName = "ai-provider:Groq";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Name => AiProviderNames.Groq;

    private AiProviderOptions Settings => aiOptions.CurrentValue.GetRequiredProvider(Name);

    public async Task<IReadOnlyList<RemoteAiModel>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var options = Settings;
        EnsureConfigured(options);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning(
                "Groq models list failed with {Status}: {Body}",
                (int)response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }

        var payload = await response.Content
            .ReadFromJsonAsync<GroqModelsResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return (payload?.Data ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new RemoteAiModel(
                ExternalId: id,
                DisplayName: FormatDisplayName(id),
                IconKey: null))
            .ToList();
    }

    public async Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        var options = Settings;
        EnsureConfigured(options);

        var modelId = request.Model.Trim();
        var chatClient = CreateChatClient(options, modelId);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, request.SystemPrompt),
            new(ChatRole.User, request.UserPrompt),
        };

        var chatOptions = new ChatOptions { ModelId = modelId };
        var response = await chatClient
            .GetResponseAsync(messages, chatOptions, cancellationToken)
            .ConfigureAwait(false);

        return new AiChatResult { Text = response.Text ?? string.Empty };
    }

    private static IChatClient CreateChatClient(AiProviderOptions options, string modelId)
    {
        var openAi = new OpenAIClient(
            new ApiKeyCredential(options.ApiKey.Trim()),
            new OpenAIClientOptions { Endpoint = new Uri(options.BaseUrl.TrimEnd('/')) });

        return openAi.GetChatClient(modelId).AsIChatClient();
    }

    private static void EnsureConfigured(AiProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException($"Ai:Providers:{AiProviderNames.Groq}:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException($"Ai:Providers:{AiProviderNames.Groq}:BaseUrl is required.");
        }
    }

    private static string FormatDisplayName(string externalId)
    {
        var raw = externalId.Replace('-', ' ').Replace('_', ' ').Trim();
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

            parts[i] = char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
        }

        return string.Join(' ', parts);
    }

    private sealed class GroqModelsResponse
    {
        [JsonPropertyName("data")]
        public List<GroqModelItem> Data { get; set; } = [];
    }

    private sealed class GroqModelItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }
}
