using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Features.Queries.Projects.GetProjectAiUsageSummary;

public sealed class GetProjectAiUsageSummaryQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<AiUsageActivity> activityRepository)
    : IQueryHandler<GetProjectAiUsageSummaryQuery, ProjectAiUsageSummaryDto>
{
    public async Task<ErrorOr<ProjectAiUsageSummaryDto>> Handle(
        GetProjectAiUsageSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing AI usage.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            filter: project => project.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var toUtc = request.ToUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var fromUtc = request.FromUtc?.ToUniversalTime() ?? toUtc.AddDays(-30);
        if (fromUtc > toUtc)
        {
            (fromUtc, toUtc) = (toUtc.AddDays(-30), fromUtc);
        }

        var query = await activityRepository.GetQuery(cancellationToken);
        var rows = await query
            .AsNoTracking()
            .Where(activity =>
                activity.ProjectId == request.ProjectId
                && activity.CreatedAt >= fromUtc
                && activity.CreatedAt <= toUtc)
            .Select(activity => new
            {
                activity.OperationType,
                activity.OperationName,
                activity.Model,
                activity.PromptTokens,
                activity.CompletionTokens,
                activity.TotalTokens,
                activity.DurationMs,
                activity.Success,
                activity.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalOps = rows.Count;
        var successful = rows.Count(row => row.Success);
        var promptTokens = rows.Sum(row => (long)(row.PromptTokens ?? 0));
        var completionTokens = rows.Sum(row => (long)(row.CompletionTokens ?? 0));
        var totalTokens = rows.Sum(row => (long)(row.TotalTokens ?? ((row.PromptTokens ?? 0) + (row.CompletionTokens ?? 0))));
        var avgLatency = totalOps == 0 ? 0d : rows.Average(row => (double)row.DurationMs);

        var byType = rows
            .GroupBy(row => row.OperationType.ToString())
            .Select(group => new AiUsageNamedCountDto
            {
                Name = group.Key,
                Count = group.Count(),
                TotalTokens = group.Sum(row => (long)(row.TotalTokens ?? 0)),
                AverageLatencyMs = group.Average(row => (double)row.DurationMs),
            })
            .OrderByDescending(item => item.Count)
            .ToList();

        var byName = rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.OperationName) ? "unspecified" : row.OperationName)
            .Select(group => new AiUsageNamedCountDto
            {
                Name = group.Key,
                Count = group.Count(),
                TotalTokens = group.Sum(row => (long)(row.TotalTokens ?? 0)),
                AverageLatencyMs = group.Average(row => (double)row.DurationMs),
            })
            .OrderByDescending(item => item.Count)
            .Take(12)
            .ToList();

        var byModel = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Model))
            .GroupBy(row => row.Model!.Trim())
            .Select(group => new AiUsageNamedCountDto
            {
                Name = group.Key,
                Count = group.Count(),
                TotalTokens = group.Sum(row => (long)(row.TotalTokens ?? 0)),
                AverageLatencyMs = group.Average(row => (double)row.DurationMs),
            })
            .OrderByDescending(item => item.TotalTokens)
            .Take(10)
            .ToList();

        var dailyLookup = rows
            .GroupBy(row => DateOnly.FromDateTime(row.CreatedAt))
            .ToDictionary(
                group => group.Key,
                group => new AiUsageDailyPointDto
                {
                    Date = group.Key,
                    Operations = group.Count(),
                    TotalTokens = group.Sum(row => (long)(row.TotalTokens ?? 0)),
                    AverageLatencyMs = group.Average(row => (double)row.DurationMs),
                });

        var daily = new List<AiUsageDailyPointDto>();
        for (var day = DateOnly.FromDateTime(fromUtc); day <= DateOnly.FromDateTime(toUtc); day = day.AddDays(1))
        {
            daily.Add(
                dailyLookup.TryGetValue(day, out var point)
                    ? point
                    : new AiUsageDailyPointDto { Date = day, Operations = 0, TotalTokens = 0, AverageLatencyMs = 0 });
        }

        return new ProjectAiUsageSummaryDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalOperations = totalOps,
            SuccessfulOperations = successful,
            SuccessRate = totalOps == 0 ? 0 : Math.Round(100d * successful / totalOps, 1),
            TotalTokens = totalTokens,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            AverageLatencyMs = Math.Round(avgLatency, 1),
            EmbeddingOperations = rows.Count(row => row.OperationType == AiUsageOperationType.Embedding),
            ToolExecutions = rows.Count(row => row.OperationType == AiUsageOperationType.ToolExecution),
            ChatCompletions = rows.Count(row => row.OperationType == AiUsageOperationType.ChatCompletion),
            FunctionCalls = rows.Count(row => row.OperationType == AiUsageOperationType.FunctionCall),
            ByOperationType = byType,
            ByOperationName = byName,
            ByModel = byModel,
            Daily = daily,
        };
    }
}
