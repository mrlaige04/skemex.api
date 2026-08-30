using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;

namespace Skemex.Infrastructure.Services.Embeddings;

/// <summary>
/// Google Gemini embedding API (<c>generativelanguage.googleapis.com</c>).
/// </summary>
public sealed class GoogleGeminiEmbeddingService(
    IHttpClientFactory httpClientFactory,
    IOptions<EmbeddingsOptions> options,
    ILogger<GoogleGeminiEmbeddingService> logger) : IEmbeddingService
{
    public const string HttpClientName = "GoogleGeminiEmbeddingService";

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    private readonly EmbeddingsOptions _options = options.Value;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var batch = await EmbedBatchAsync([text], cancellationToken).ConfigureAwait(false);
        return batch[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        ValidateConfiguration();

        if (texts.Count == 1)
        {
            var single = await EmbedSingleWithRetryAsync(texts[0], cancellationToken).ConfigureAwait(false);
            return [single];
        }

        var results = new float[texts.Count][];
        var batchSize = Math.Clamp(_options.BatchSize, 1, 100);

        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var batchEmbeddings = await EmbedBatchWithRetryAsync(batch, cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < batchEmbeddings.Count; i++)
            {
                results[offset + i] = batchEmbeddings[i];
            }
        }

        return results;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Embeddings:ApiKey is not configured. Set it in appsettings.json or environment variables.");
        }

        if (_options.Dimensions is not (768 or 1536 or 3072))
        {
            throw new InvalidOperationException(
                $"Embeddings:Dimensions must be 768, 1536, or 3072 for {_options.Model}.");
        }
    }

    private GeminiEmbedContentRequest CreateEmbedRequest(string text) =>
        new()
        {
            Model = ResolveModelResource(),
            Content = new GeminiContent
            {
                Parts = [new GeminiPart { Text = text }],
            },
            OutputDimensionality = _options.Dimensions,
        };

    private string ResolveModelResource()
    {
        var model = _options.Model.Trim();
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model
            : $"models/{model}";
    }

    private string ResolveModelPathSegment()
    {
        var model = _options.Model.Trim();
        return model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;
    }

    private async Task<float[]> EmbedSingleWithRetryAsync(string text, CancellationToken cancellationToken)
    {
        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/models/{ResolveModelPathSegment()}:embedContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        var payload = CreateEmbedRequest(text);

        var response = await SendWithRetryAsync(
            () => CreateClient().PostAsJsonAsync(url, payload, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<GeminiEmbedContentResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var values = body?.Embedding?.Values;
        if (values is null || values.Length != _options.Dimensions)
        {
            throw new InvalidOperationException("Google Gemini embedding API returned an invalid embedding.");
        }

        return values;
    }

    private async Task<IReadOnlyList<float[]>> EmbedBatchWithRetryAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken)
    {
        var url =
            $"{_options.BaseUrl.TrimEnd('/')}/models/{ResolveModelPathSegment()}:batchEmbedContents?key={Uri.EscapeDataString(_options.ApiKey)}";

        var payload = new GeminiBatchEmbedRequest
        {
            Requests = texts.Select(CreateEmbedRequest).ToList(),
        };

        var response = await SendWithRetryAsync(
            () => CreateClient().PostAsJsonAsync(url, payload, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var values = body?.Embeddings?
            .Select(entry => entry.Values)
            .ToList();

        return ValidateEmbedding(values, texts.Count);
    }

    private IReadOnlyList<float[]> ValidateEmbedding(IList<float[]>? embeddings, int expectedCount)
    {
        if (embeddings is null || embeddings.Count != expectedCount)
        {
            throw new InvalidOperationException("Google Gemini embedding API returned an unexpected batch size.");
        }

        foreach (var embedding in embeddings)
        {
            if (embedding.Length != _options.Dimensions)
            {
                throw new InvalidOperationException(
                    $"Google Gemini embedding API returned {embedding.Length} dimensions; expected {_options.Dimensions}.");
            }
        }

        return embeddings.ToList();
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(_options.MaxRetryAttempts, 1, 6);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await send().ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                if (!IsTransientStatus(response.StatusCode) || attempt == maxAttempts)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    logger.LogError(
                        "Google Gemini embedding request failed with status {StatusCode}: {Body}",
                        (int)response.StatusCode,
                        errorBody);
                    response.EnsureSuccessStatusCode();
                }

                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts && IsTransientHttpFailure(ex))
            {
                response?.Dispose();
                logger.LogWarning(ex, "Transient Google Gemini embedding failure on attempt {Attempt}.", attempt);
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < maxAttempts)
            {
                response?.Dispose();
                logger.LogWarning(ex, "Transient Google Gemini embedding failure on attempt {Attempt}.", attempt);
            }

            await Task.Delay(RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)], cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidOperationException("Google Gemini embedding request failed after retries.");
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError;

    private static bool IsTransientHttpFailure(HttpRequestException exception)
    {
        var message = exception.Message;
        return message.Contains("408", StringComparison.Ordinal)
            || message.Contains("429", StringComparison.Ordinal)
            || message.Contains("500", StringComparison.Ordinal)
            || message.Contains("502", StringComparison.Ordinal)
            || message.Contains("503", StringComparison.Ordinal)
            || message.Contains("504", StringComparison.Ordinal);
    }

    private static bool IsTransientException(Exception exception) =>
        exception is TaskCanceledException;

    private HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

    private sealed class GeminiEmbedContentRequest
    {
        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; } = new();

        [JsonPropertyName("outputDimensionality")]
        public int? OutputDimensionality { get; set; }
    }

    private sealed class GeminiBatchEmbedRequest
    {
        [JsonPropertyName("requests")]
        public List<GeminiEmbedContentRequest> Requests { get; set; } = [];
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class GeminiEmbedContentResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbedding? Embedding { get; set; }
    }

    private sealed class GeminiBatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbedding>? Embeddings { get; set; }
    }

    private sealed class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public float[] Values { get; set; } = [];
    }
}
