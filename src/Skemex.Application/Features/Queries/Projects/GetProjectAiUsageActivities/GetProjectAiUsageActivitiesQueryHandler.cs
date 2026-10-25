using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Models.Ai.Agents;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Models.Ai.Providers;
using Skemex.Application.Models.Ai.Tools;
using Skemex.Application.Models.Ai.Usage;

namespace Skemex.Application.Features.Queries.Projects.GetProjectAiUsageActivities;

public sealed class GetProjectAiUsageActivitiesQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<AiUsageActivity> activityRepository)
    : IQueryHandler<GetProjectAiUsageActivitiesQuery, PaginatedList<AiUsageActivityDto>>
{
    public async Task<ErrorOr<PaginatedList<AiUsageActivityDto>>> Handle(
        GetProjectAiUsageActivitiesQuery request,
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
        var search = request.Search?.Trim();
        var operationName = request.OperationName?.Trim();
        var fromUtc = request.FromUtc?.ToUniversalTime();
        var toUtc = request.ToUtc?.ToUniversalTime();

        var query = await activityRepository.GetQuery(cancellationToken);
        query = query
            .AsNoTracking()
            .Include(activity => activity.User)
            .Where(activity => activity.ProjectId == request.ProjectId);

        if (request.OperationType is { } operationType)
        {
            query = query.Where(activity => activity.OperationType == operationType);
        }

        if (!string.IsNullOrEmpty(operationName))
        {
            query = query.Where(activity => activity.OperationName == operationName);
        }

        if (request.Success is { } success)
        {
            query = query.Where(activity => activity.Success == success);
        }

        if (request.UserId is { } userId)
        {
            query = query.Where(activity => activity.UserId == userId);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(activity => activity.CreatedAt >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(activity => activity.CreatedAt <= to);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(activity =>
                activity.OperationName.ToLower().Contains(term)
                || (activity.Model != null && activity.Model.ToLower().Contains(term))
                || (activity.Provider != null && activity.Provider.ToLower().Contains(term))
                || (activity.ToolName != null && activity.ToolName.ToLower().Contains(term))
                || (activity.User.Email != null && activity.User.Email.ToLower().Contains(term))
                || activity.User.FirstName.ToLower().Contains(term)
                || activity.User.LastName.ToLower().Contains(term));
        }

        query = ApplySort(query, request.Sort);

        var totalItems = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = entities.Select(MapToDto).ToList();
        return new PaginatedList<AiUsageActivityDto>(items, totalItems, pageNumber, pageSize);
    }

    private static AiUsageActivityDto MapToDto(AiUsageActivity activity)
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

    private static IQueryable<AiUsageActivity> ApplySort(IQueryable<AiUsageActivity> query, string? sort) =>
        (sort ?? AiUsageActivitySort.CreatedAtDesc) switch
        {
            AiUsageActivitySort.CreatedAtAsc =>
                query.OrderBy(activity => activity.CreatedAt).ThenBy(activity => activity.Id),
            AiUsageActivitySort.TotalTokensDesc =>
                query.OrderByDescending(activity => activity.TotalTokens ?? 0)
                    .ThenByDescending(activity => activity.CreatedAt),
            AiUsageActivitySort.TotalTokensAsc =>
                query.OrderBy(activity => activity.TotalTokens ?? 0)
                    .ThenByDescending(activity => activity.CreatedAt),
            AiUsageActivitySort.DurationMsDesc =>
                query.OrderByDescending(activity => activity.DurationMs)
                    .ThenByDescending(activity => activity.CreatedAt),
            AiUsageActivitySort.DurationMsAsc =>
                query.OrderBy(activity => activity.DurationMs)
                    .ThenByDescending(activity => activity.CreatedAt),
            _ => query.OrderByDescending(activity => activity.CreatedAt).ThenByDescending(activity => activity.Id),
        };
}
