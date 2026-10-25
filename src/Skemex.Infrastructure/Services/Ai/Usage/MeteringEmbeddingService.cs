using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Enums;
using Skemex.Domain.Services;
using Skemex.Infrastructure.Services.Embeddings;

namespace Skemex.Infrastructure.Services.Ai.Usage;

/// <summary>
/// Decorator over <see cref="IEmbeddingService"/> that meters embedding generation.
/// </summary>
public sealed class MeteringEmbeddingService(
    GoogleGeminiEmbeddingService inner,
    IAiUsageMeteringService metering,
    IAiUsageQuotaEvaluator quotaEvaluator,
    ICurrentUser currentUser,
    IOptions<EmbeddingsOptions> embeddingsOptions,
    ILogger<MeteringEmbeddingService> logger) : IEmbeddingService
{
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

        var operationName = ResolveOperationName(AiUsageOperationNames.UnspecifiedEmbedding);
        var model = embeddingsOptions.Value.Model;
        await EnsureQuotaAsync(operationName, model, cancellationToken).ConfigureAwait(false);

        var estimatedPromptTokens = EstimatePromptTokens(texts);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await inner.EmbedBatchAsync(texts, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await metering.RecordAsync(
                new AiUsageRecord
                {
                    OperationType = AiUsageOperationType.Embedding,
                    OperationName = operationName,
                    Provider = "google-gemini",
                    Model = model,
                    PromptTokens = estimatedPromptTokens,
                    CompletionTokens = 0,
                    TotalTokens = estimatedPromptTokens,
                    InputItemCount = texts.Count,
                    Success = true,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                },
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await metering.RecordAsync(
                new AiUsageRecord
                {
                    OperationType = AiUsageOperationType.Embedding,
                    OperationName = operationName,
                    Provider = "google-gemini",
                    Model = model,
                    PromptTokens = estimatedPromptTokens,
                    CompletionTokens = 0,
                    TotalTokens = estimatedPromptTokens,
                    InputItemCount = texts.Count,
                    Success = false,
                    ErrorMessage = ex.Message,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                },
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async Task EnsureQuotaAsync(
        string operationName,
        string? model,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        var userId = currentUser.GetUserId();
        if (tenantId is null || userId is null)
        {
            return;
        }

        var check = await quotaEvaluator
            .EnsureAllowedAsync(
                new AiUsageQuotaCheck
                {
                    TenantId = tenantId.Value,
                    UserId = userId.Value,
                    OperationName = operationName,
                    Model = model,
                    ProjectId = AmbientAiUsageContext.Current?.ProjectId,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (check.IsError)
        {
            logger.LogWarning(
                "Embedding quota denied for {OperationName}: {Error}",
                operationName,
                check.FirstError.Description);
            throw new InvalidOperationException(check.FirstError.Description);
        }
    }

    private static string ResolveOperationName(string fallback)
    {
        var ambient = AmbientAiUsageContext.Current?.OperationName;
        return string.IsNullOrWhiteSpace(ambient) ? fallback : ambient.Trim();
    }

    /// <summary>
    /// Rough heuristic when the embedding API does not return token counts (~4 chars/token).
    /// </summary>
    private static int EstimatePromptTokens(IReadOnlyList<string> texts)
    {
        var chars = texts.Sum(text => text?.Length ?? 0);
        return Math.Max(1, (chars + 3) / 4);
    }
}
