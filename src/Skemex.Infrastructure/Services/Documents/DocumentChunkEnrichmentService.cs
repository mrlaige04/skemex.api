using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Application.Services.Documents;

namespace Skemex.Infrastructure.Services.Documents;

public sealed class DocumentChunkEnrichmentService(
    IAiService aiService,
    IAiSemanticRetry semanticRetry,
    IOptions<DocumentIngestionOptions> options,
    ILogger<DocumentChunkEnrichmentService> logger) : IDocumentChunkEnrichmentService
{
    private const string EnrichmentSystemPrompt =
        "You enrich document chunks for hybrid RAG indexing.\n" +
        "Reply with ONLY a JSON array. No markdown fences. No commentary.\n" +
        "The array MUST have exactly one object per input chunk, in the same order.\n" +
        "Each object shape:\n" +
        "{\"englishText\": string|null, \"keywords\": string[]}\n" +
        "- englishText: concise, accurate technical English translation that preserves technical terms, " +
        "system identifiers, error codes, and constraints. Return null when the chunk is already English.\n" +
        "- keywords: 5 to 10 high-signal technical English keywords, error codes, domain entities, or short phrases. " +
        "Prefer lowercase tokens except for proper identifiers/codes.\n" +
        "Do not invent facts that are not present in the chunk.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<EnrichedChunk>> EnrichAsync(
        IReadOnlyList<string> chunkTexts,
        string? modelExternalId,
        CancellationToken cancellationToken = default)
    {
        if (chunkTexts.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(modelExternalId))
        {
            throw new InvalidOperationException(
                "No AI model is configured for document chunk enrichment. " +
                "Set DocumentIngestion:EnrichmentModel in appsettings.");
        }

        var batchSize = Math.Clamp(options.Value.EnrichmentBatchSize, 5, 8);
        var results = new List<EnrichedChunk>(chunkTexts.Count);
        var model = modelExternalId.Trim();

        for (var offset = 0; offset < chunkTexts.Count; offset += batchSize)
        {
            var batch = chunkTexts
                .Skip(offset)
                .Take(batchSize)
                .ToList();

            var enrichedBatch = await EnrichBatchAsync(batch, model, cancellationToken)
                .ConfigureAwait(false);
            results.AddRange(enrichedBatch);
        }

        return results;
    }

    private async Task<IReadOnlyList<EnrichedChunk>> EnrichBatchAsync(
        IReadOnlyList<string> batch,
        string modelExternalId,
        CancellationToken cancellationToken)
    {
        var userPrompt = BuildUserPrompt(batch);

        return await semanticRetry
            .ExecuteAsync(
                async ct =>
                {
                    var completion = await aiService.CompleteAsync(
                        new AiCompletionRequest
                        {
                            SystemPrompt = EnrichmentSystemPrompt,
                            UserPrompt = userPrompt,
                            Model = modelExternalId,
                            PreferJsonObject = false,
                        },
                        ct);

                    return ParseBatchResponse(batch, completion.Content);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildUserPrompt(IReadOnlyList<string> batch)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Enrich the following {batch.Count} document chunk(s). Return a JSON array of length {batch.Count}.");
        builder.AppendLine();

        for (var i = 0; i < batch.Count; i++)
        {
            builder.Append('[').Append(i).AppendLine("]");
            builder.AppendLine(batch[i]);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private IReadOnlyList<EnrichedChunk> ParseBatchResponse(
        IReadOnlyList<string> batch,
        string? rawContent)
    {
        var json = ExtractJsonArray(rawContent);
        if (json is null)
        {
            throw new AiSemanticValidationException("Enrichment response did not contain a JSON array.");
        }

        List<EnrichmentDto>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<EnrichmentDto>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize chunk enrichment JSON.");
            throw new AiSemanticValidationException("Enrichment JSON could not be parsed.");
        }

        if (items is null || items.Count != batch.Count)
        {
            throw new AiSemanticValidationException(
                $"Enrichment array length mismatch: expected {batch.Count}, got {items?.Count ?? 0}.");
        }

        var results = new List<EnrichedChunk>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            var item = items[i];
            var keywords = NormalizeKeywords(item.Keywords);
            if (keywords.Count is < 5 or > 10)
            {
                throw new AiSemanticValidationException(
                    $"Chunk [{i}] keywords must contain 5–10 items (got {keywords.Count}).");
            }

            var english = string.IsNullOrWhiteSpace(item.EnglishText)
                ? null
                : item.EnglishText.Trim();

            results.Add(new EnrichedChunk
            {
                Text = batch[i],
                EnglishText = english,
                Keywords = keywords,
            });
        }

        return results;
    }

    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return [];
        }

        return keywords
            .Select(k => k?.Trim() ?? string.Empty)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static string? ExtractJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = trimmed.IndexOf('\n');
            if (firstNl >= 0)
            {
                trimmed = trimmed[(firstNl + 1)..];
            }

            var fence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0)
            {
                trimmed = trimmed[..fence];
            }

            trimmed = trimmed.Trim();
        }

        var start = trimmed.IndexOf('[');
        var end = trimmed.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private sealed class EnrichmentDto
    {
        public string? EnglishText { get; set; }
        public List<string>? Keywords { get; set; }
    }
}
