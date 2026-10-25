using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Services.Ai.Usage;

public sealed class AiUsageMeteringService(
    ITenantRepository<AiUsageActivity> activityRepository,
    ICurrentUser currentUser,
    ILogger<AiUsageMeteringService> logger) : IAiUsageMeteringService
{
    public async Task RecordAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = BuildEntity(record);
            if (entity is null)
            {
                return;
            }

            await activityRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist AI usage activity {OperationName} ({OperationType}).",
                record.OperationName,
                record.OperationType);
        }
    }

    public async Task<Guid?> RecordToolExecutionAsync(
        AiUsageRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = BuildEntity(record);
            if (entity is null)
            {
                return null;
            }

            await activityRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            return entity.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to persist AI tool execution activity {OperationName}.",
                record.OperationName);
            return null;
        }
    }

    private AiUsageActivity? BuildEntity(AiUsageRecord record)
    {
        var tenantId = currentUser.GetTenantId();
        var userId = currentUser.GetUserId();
        if (tenantId is null || userId is null)
        {
            logger.LogWarning(
                "Skipping AI usage activity {OperationName}: tenant/user context is missing.",
                record.OperationName);
            return null;
        }

        var ambient = AmbientAiUsageContext.Current;
        var correlationId = record.CorrelationId
            ?? ambient?.CorrelationId
            ?? Guid.NewGuid();

        return new AiUsageActivity
        {
            Id = record.ActivityId ?? Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = userId.Value,
            CorrelationId = correlationId,
            ParentActivityId = record.ParentActivityId ?? ambient?.ParentActivityId,
            OperationType = record.OperationType,
            OperationName = string.IsNullOrWhiteSpace(record.OperationName)
                ? "unspecified"
                : record.OperationName.Trim(),
            Provider = TrimOrNull(record.Provider),
            Model = TrimOrNull(record.Model),
            ToolName = TrimOrNull(record.ToolName ?? ambient?.ToolName),
            PromptTokens = record.PromptTokens,
            CompletionTokens = record.CompletionTokens,
            TotalTokens = record.TotalTokens
                ?? SumTokens(record.PromptTokens, record.CompletionTokens),
            InputItemCount = record.InputItemCount,
            Success = record.Success,
            ErrorMessage = Truncate(record.ErrorMessage, 2000),
            DurationMs = Math.Max(0, record.DurationMs),
            MetadataJson = record.MetadataJson,
            ProjectId = record.ProjectId ?? ambient?.ProjectId,
            AiChatId = record.AiChatId ?? ambient?.AiChatId,
            AiChatMessageId = record.AiChatMessageId ?? ambient?.AiChatMessageId,
            AiAgentJobId = record.AiAgentJobId ?? ambient?.AiAgentJobId,
            ProjectDocumentId = record.ProjectDocumentId ?? ambient?.ProjectDocumentId,
            AiExecutionId = record.AiExecutionId ?? ambient?.AiExecutionId,
        };
    }

    private static int? SumTokens(int? prompt, int? completion)
    {
        if (prompt is null && completion is null)
        {
            return null;
        }

        return (prompt ?? 0) + (completion ?? 0);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
