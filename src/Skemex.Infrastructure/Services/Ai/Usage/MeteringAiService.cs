using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Enums;
using Skemex.Domain.Services;
using Skemex.Domain.Consts;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Infrastructure.Services.Ai.Usage;

/// <summary>
/// Decorator over <see cref="IAiService"/> that meters completions and function calls
/// without changing business-call-site logic.
/// </summary>
public sealed class MeteringAiService(
    AiService inner,
    IAiUsageMeteringService metering,
    IAiUsageQuotaEvaluator quotaEvaluator,
    ICurrentUser currentUser,
    ILogger<MeteringAiService> logger) : IAiService
{
    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var operationName = ResolveOperationName(AiUsageOperationNames.UnspecifiedCompletion);
        await EnsureQuotaAsync(operationName, request.Model, cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await metering.RecordAsync(
                BuildRecord(
                    AiUsageOperationType.ChatCompletion,
                    operationName,
                    result.Provider,
                    result.Model ?? request.Model,
                    result.Usage,
                    success: true,
                    error: null,
                    stopwatch.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await metering.RecordAsync(
                BuildRecord(
                    AiUsageOperationType.ChatCompletion,
                    operationName,
                    provider: null,
                    model: request.Model,
                    usage: null,
                    success: false,
                    error: ex.Message,
                    stopwatch.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<AiFunctionCallResult> FunctionCallAsync(
        AiFunctionCallRequest request,
        CancellationToken cancellationToken = default)
    {
        var operationName = ResolveOperationName(AiUsageOperationNames.UnspecifiedFunctionCall);
        await EnsureQuotaAsync(operationName, request.Model, cancellationToken).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await inner.FunctionCallAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            await metering.RecordAsync(
                BuildRecord(
                    AiUsageOperationType.FunctionCall,
                    operationName,
                    result.Provider,
                    result.Model ?? request.Model,
                    result.Usage,
                    success: true,
                    error: null,
                    stopwatch.ElapsedMilliseconds,
                    toolName: result.FunctionCall?.Name),
                cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await metering.RecordAsync(
                BuildRecord(
                    AiUsageOperationType.FunctionCall,
                    operationName,
                    provider: null,
                    model: request.Model,
                    usage: null,
                    success: false,
                    error: ex.Message,
                    stopwatch.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        inner.ListModelsAsync(forceRefresh, cancellationToken);

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
                "AI quota denied for {OperationName}: {Error}",
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

    private static AiUsageRecord BuildRecord(
        AiUsageOperationType operationType,
        string operationName,
        string? provider,
        string? model,
        AiTokenUsage? usage,
        bool success,
        string? error,
        long durationMs,
        string? toolName = null) =>
        new()
        {
            OperationType = operationType,
            OperationName = operationName,
            Provider = provider,
            Model = model,
            ToolName = toolName ?? AmbientAiUsageContext.Current?.ToolName,
            PromptTokens = usage?.PromptTokens,
            CompletionTokens = usage?.CompletionTokens,
            TotalTokens = usage?.TotalTokens,
            Success = success,
            ErrorMessage = error,
            DurationMs = durationMs,
        };
}
