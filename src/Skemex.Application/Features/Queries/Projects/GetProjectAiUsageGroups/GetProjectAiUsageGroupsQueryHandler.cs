using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.Projects.GetProjectAiUsageActivities;
using Skemex.Domain.Abstractions;
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

namespace Skemex.Application.Features.Queries.Projects.GetProjectAiUsageGroups;

public sealed class GetProjectAiUsageGroupsQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<AiExecution> executionRepository,
    ITenantRepository<AiUsageActivity> activityRepository)
    : IQueryHandler<GetProjectAiUsageGroupsQuery, PaginatedList<AiUsageGroupDto>>
{
    public async Task<ErrorOr<PaginatedList<AiUsageGroupDto>>> Handle(
        GetProjectAiUsageGroupsQuery request,
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

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

        var query = await BuildFilteredQuery(request, cancellationToken).ConfigureAwait(false);

        query = (request.Sort ?? AiUsageActivitySort.CreatedAtDesc) switch
        {
            AiUsageActivitySort.CreatedAtAsc =>
                query.OrderBy(execution => execution.StartedAt ?? execution.CreatedAt)
                    .ThenBy(execution => execution.Id),
            AiUsageActivitySort.TotalTokensDesc =>
                query.OrderByDescending(execution => execution.TotalTokens)
                    .ThenByDescending(execution => execution.StartedAt ?? execution.CreatedAt),
            AiUsageActivitySort.TotalTokensAsc =>
                query.OrderBy(execution => execution.TotalTokens)
                    .ThenByDescending(execution => execution.StartedAt ?? execution.CreatedAt),
            AiUsageActivitySort.DurationMsDesc =>
                query.OrderByDescending(execution => execution.DurationMs ?? 0)
                    .ThenByDescending(execution => execution.StartedAt ?? execution.CreatedAt),
            AiUsageActivitySort.DurationMsAsc =>
                query.OrderBy(execution => execution.DurationMs ?? 0)
                    .ThenByDescending(execution => execution.StartedAt ?? execution.CreatedAt),
            _ => query.OrderByDescending(execution => execution.StartedAt ?? execution.CreatedAt)
                .ThenByDescending(execution => execution.Id),
        };

        var totalGroups = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(execution => execution.User)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (page.Count == 0)
        {
            return new PaginatedList<AiUsageGroupDto>([], totalGroups, pageNumber, pageSize);
        }

        var executionIds = page.Select(execution => execution.Id).ToList();
        var activities = await activityRepository.GetAllAsync(
            filter: activity =>
                activity.AiExecutionId != null && executionIds.Contains(activity.AiExecutionId.Value),
            include: q => q.Include(activity => activity.User),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var byExecution = activities
            .GroupBy(activity => activity.AiExecutionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(activity => activity.CreatedAt).ThenBy(activity => activity.Id).ToList());

        var groups = page
            .Select(execution => BuildGroup(
                execution,
                byExecution.GetValueOrDefault(execution.Id) ?? []))
            .ToList();

        return new PaginatedList<AiUsageGroupDto>(groups, totalGroups, pageNumber, pageSize);
    }

    private async Task<IQueryable<AiExecution>> BuildFilteredQuery(
        GetProjectAiUsageGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search?.Trim();
        var operationName = request.OperationName?.Trim();
        var fromUtc = request.FromUtc?.ToUniversalTime();
        var toUtc = request.ToUtc?.ToUniversalTime();

        var query = await executionRepository.GetQuery(cancellationToken);
        query = query
            .AsNoTracking()
            .Where(execution => execution.ProjectId == request.ProjectId);

        if (request.OperationType is { } operationType)
        {
            var activitiesByType = await activityRepository.GetQuery(cancellationToken);
            var matchingIds = activitiesByType
                .AsNoTracking()
                .Where(activity =>
                    activity.ProjectId == request.ProjectId
                    && activity.AiExecutionId != null
                    && activity.OperationType == operationType)
                .Select(activity => activity.AiExecutionId!.Value);
            query = query.Where(execution => matchingIds.Contains(execution.Id));
        }

        if (!string.IsNullOrEmpty(operationName))
        {
            var activitiesByName = await activityRepository.GetQuery(cancellationToken);
            var matchingIds = activitiesByName
                .AsNoTracking()
                .Where(activity =>
                    activity.ProjectId == request.ProjectId
                    && activity.AiExecutionId != null
                    && activity.OperationName == operationName)
                .Select(activity => activity.AiExecutionId!.Value);
            query = query.Where(execution => matchingIds.Contains(execution.Id));
        }

        if (request.Success is { } success)
        {
            query = query.Where(execution => execution.Success == success);
        }

        if (request.UserId is { } userId)
        {
            query = query.Where(execution => execution.UserId == userId);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(execution => (execution.StartedAt ?? execution.CreatedAt) >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(execution => (execution.StartedAt ?? execution.CreatedAt) <= to);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(execution =>
                execution.Title.ToLower().Contains(term)
                || (execution.ToolName != null && execution.ToolName.ToLower().Contains(term))
                || (execution.Model != null && execution.Model.ToLower().Contains(term))
                || (execution.UserInputPreview != null && execution.UserInputPreview.ToLower().Contains(term))
                || (execution.User.Email != null && execution.User.Email.ToLower().Contains(term))
                || execution.User.FirstName.ToLower().Contains(term)
                || execution.User.LastName.ToLower().Contains(term));
        }

        return query;
    }

    private static AiUsageGroupDto BuildGroup(
        AiExecution execution,
        IReadOnlyList<AiUsageActivity> activities)
    {
        var user = execution.User;
        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        var operationTypes = activities
            .Select(activity => activity.OperationType.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var toolNames = activities
            .Select(activity => activity.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (toolNames.Count == 0 && !string.IsNullOrWhiteSpace(execution.ToolName))
        {
            toolNames = [execution.ToolName];
        }

        var startedAt = execution.StartedAt ?? execution.CreatedAt;
        var endedAt = execution.EndedAt;
        var durationMs = execution.DurationMs
            ?? (endedAt is { } end ? Math.Max(0, (long)(end - startedAt).TotalMilliseconds) : 0L);

        var success = execution.Success
            ?? execution.Status switch
            {
                AiExecutionStatus.Succeeded => true,
                AiExecutionStatus.Failed => false,
                _ => activities.Count == 0 || activities.All(activity => activity.Success),
            };

        return new AiUsageGroupDto
        {
            Id = execution.Id,
            CorrelationId = execution.Id,
            Title = execution.Title,
            Source = execution.Source.ToString(),
            ContextLabel = FormatSource(execution.Source),
            Status = execution.Status.ToString(),
            StartedAt = startedAt,
            EndedAt = endedAt,
            OperationCount = execution.OperationCount > 0 ? execution.OperationCount : activities.Count,
            ToolCount = toolNames.Count,
            TotalTokens = execution.TotalTokens > 0
                ? execution.TotalTokens
                : activities.Sum(activity =>
                    (long)(activity.TotalTokens ?? ((activity.PromptTokens ?? 0) + (activity.CompletionTokens ?? 0)))),
            PromptTokens = execution.PromptTokens > 0
                ? execution.PromptTokens
                : activities.Sum(activity => (long)(activity.PromptTokens ?? 0)),
            CompletionTokens = execution.CompletionTokens > 0
                ? execution.CompletionTokens
                : activities.Sum(activity => (long)(activity.CompletionTokens ?? 0)),
            DurationMs = durationMs,
            Success = success,
            UserId = execution.UserId,
            UserDisplayName = string.IsNullOrEmpty(fullName) ? (user.Email ?? "User") : fullName,
            UserEmail = user.Email,
            OperationTypes = operationTypes,
            ToolNames = toolNames,
            Activities = activities.Select(MapActivity).ToList(),
        };
    }

    private static string FormatSource(AiExecutionSource source) => source switch
    {
        AiExecutionSource.Chat => "Chat",
        AiExecutionSource.Ui => "UI",
        AiExecutionSource.Api => "API",
        AiExecutionSource.Documents => "Documents",
        _ => source.ToString(),
    };

    private static AiUsageActivityDto MapActivity(AiUsageActivity activity)
    {
        var fullName = $"{activity.User.FirstName} {activity.User.LastName}".Trim();
        return new AiUsageActivityDto
        {
            Id = activity.Id,
            CorrelationId = activity.CorrelationId,
            ParentActivityId = activity.ParentActivityId,
            OperationType = activity.OperationType.ToString(),
            OperationName = activity.OperationName,
            Provider = activity.Provider,
            Model = activity.Model,
            ToolName = activity.ToolName,
            PromptTokens = activity.PromptTokens,
            CompletionTokens = activity.CompletionTokens,
            TotalTokens = activity.TotalTokens,
            InputItemCount = activity.InputItemCount,
            Success = activity.Success,
            ErrorMessage = activity.ErrorMessage,
            DurationMs = activity.DurationMs,
            CreatedAt = activity.CreatedAt,
            UserId = activity.UserId,
            UserDisplayName = string.IsNullOrEmpty(fullName)
                ? (activity.User.Email ?? "User")
                : fullName,
            UserEmail = activity.User.Email,
        };
    }
}
