using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai.Usage;

public sealed class AiExecutionLifecycle(
    ITenantRepository<AiExecution> executionRepository,
    ITenantRepository<AiUsageActivity> activityRepository,
    ILogger<AiExecutionLifecycle> logger) : IAiExecutionLifecycle
{
    public async Task<AiExecution> CreatePendingAsync(
        AiExecutionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var preview = request.UserInput?.Trim();
        if (!string.IsNullOrEmpty(preview) && preview.Length > 500)
        {
            preview = preview[..497].TrimEnd() + "…";
        }

        var execution = new AiExecution
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            UserId = request.UserId,
            ProjectId = request.ProjectId,
            AiChatId = request.AiChatId,
            AiAgentJobId = request.AiAgentJobId,
            ProjectDocumentId = request.ProjectDocumentId,
            Source = request.Source,
            Status = AiExecutionStatus.Pending,
            Title = string.IsNullOrWhiteSpace(request.Title)
                ? ResolveDefaultTitle(request.Source, request.ToolName)
                : request.Title.Trim(),
            ToolName = TrimOrNull(request.ToolName),
            Model = TrimOrNull(request.Model),
            UserInputPreview = string.IsNullOrWhiteSpace(preview) ? null : preview,
        };

        await executionRepository.AddAsync(execution, cancellationToken).ConfigureAwait(false);
        return execution;
    }

    public async Task MarkRunningAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var execution = await executionRepository.GetAsync(
            filter: entry => entry.Id == executionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (execution is null)
        {
            logger.LogWarning("AI execution {ExecutionId} not found when marking running.", executionId);
            return;
        }

        execution.Status = AiExecutionStatus.Running;
        execution.StartedAt ??= DateTime.UtcNow;
        execution.ErrorMessage = null;
        execution.Success = null;
        await executionRepository.UpdateAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteAsync(
        Guid executionId,
        AiExecutionCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var execution = await executionRepository.GetAsync(
            filter: entry => entry.Id == executionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (execution is null)
        {
            logger.LogWarning("AI execution {ExecutionId} not found when completing.", executionId);
            return;
        }

        var endedAt = DateTime.UtcNow;
        var startedAt = execution.StartedAt ?? execution.CreatedAt;

        var activities = await activityRepository.GetAllAsync(
            filter: activity => activity.AiExecutionId == executionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        execution.EndedAt = endedAt;
        execution.StartedAt = startedAt;
        execution.DurationMs = Math.Max(0, (long)(endedAt - startedAt).TotalMilliseconds);
        execution.Status = request.Success ? AiExecutionStatus.Succeeded : AiExecutionStatus.Failed;
        execution.Success = request.Success;
        execution.ErrorMessage = Truncate(request.ErrorMessage, 2000);
        execution.OperationCount = activities.Count;
        execution.PromptTokens = activities.Sum(activity => (long)(activity.PromptTokens ?? 0));
        execution.CompletionTokens = activities.Sum(activity => (long)(activity.CompletionTokens ?? 0));
        execution.TotalTokens = activities.Sum(activity =>
            (long)(activity.TotalTokens ?? ((activity.PromptTokens ?? 0) + (activity.CompletionTokens ?? 0))));

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            execution.Title = request.Title.Trim();
        }
        else
        {
            execution.Title = ResolveTitleFromActivities(activities, execution);
        }

        if (!string.IsNullOrWhiteSpace(request.ToolName))
        {
            execution.ToolName = request.ToolName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            execution.Model = request.Model.Trim();
        }
        else
        {
            execution.Model ??= activities
                .Select(activity => activity.Model)
                .FirstOrDefault(model => !string.IsNullOrWhiteSpace(model));
        }

        await executionRepository.UpdateAsync(execution, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveDefaultTitle(AiExecutionSource source, string? toolName)
    {
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            return Humanize(toolName);
        }

        return source switch
        {
            AiExecutionSource.Chat => "Chat",
            AiExecutionSource.Documents => "Document indexing",
            AiExecutionSource.Ui => "UI action",
            _ => "AI execution",
        };
    }

    private static string ResolveTitleFromActivities(
        IReadOnlyList<AiUsageActivity> activities,
        AiExecution execution)
    {
        var names = activities
            .Select(activity => activity.OperationName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var tools = activities
            .Select(activity => activity.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Contains("project_qa") || tools.Contains("project_qa"))
        {
            return "Project Q&A";
        }

        if (names.Contains("task_decomposition") || tools.Contains("task_decomposition"))
        {
            return "Task decomposition";
        }

        if (names.Contains("chunk_enrichment") || names.Contains("document_embedding"))
        {
            return "Document indexing";
        }

        if (!string.IsNullOrWhiteSpace(execution.ToolName))
        {
            return Humanize(execution.ToolName);
        }

        if (names.Contains("stage1_routing"))
        {
            return "Intent routing";
        }

        return execution.Title;
    }

    private static string Humanize(string value)
    {
        var parts = value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            parts[i] = part.Length <= 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
        }

        return string.Join(' ', parts);
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
