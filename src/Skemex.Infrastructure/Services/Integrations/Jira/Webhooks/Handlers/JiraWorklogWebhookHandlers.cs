using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

public sealed class JiraWorklogCreatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger<JiraWorklogCreatedWebhookHandler> logger)
    : JiraWorklogWebhookHandlerBase(
        syncHelper,
        workLogRepository,
        taskRepository,
        mappingRepository,
        projectUserRepository,
        currentUser,
        logger),
      IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.WorklogCreated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(context, cancellationToken);
}

public sealed class JiraWorklogUpdatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger<JiraWorklogUpdatedWebhookHandler> logger)
    : JiraWorklogWebhookHandlerBase(
        syncHelper,
        workLogRepository,
        taskRepository,
        mappingRepository,
        projectUserRepository,
        currentUser,
        logger),
      IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.WorklogUpdated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(context, cancellationToken);
}

public sealed class JiraWorklogDeletedWebhookHandler(
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ILogger<JiraWorklogDeletedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.WorklogDeleted;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var worklogId = ReadWorklogId(context.Payload);
        if (string.IsNullOrWhiteSpace(worklogId))
        {
            return Result.Success;
        }

        var mapping = await mappingRepository.GetAsync(
            filter: m =>
                m.IntegrationConnectionId == context.Connection.Id
                && m.LocalEntityType == "TaskWorkLog"
                && m.ExternalEntityId == worklogId,
            cancellationToken: cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var workLog = await workLogRepository.GetAsync(
            filter: w => w.Id == mapping.LocalEntityId,
            cancellationToken: cancellationToken);
        await mappingRepository.DeleteAsync(mapping, cancellationToken);
        if (workLog is not null)
        {
            var task = await taskRepository.GetAsync(
                filter: t => t.Id == workLog.TaskId,
                cancellationToken: cancellationToken);
            if (task is not null)
            {
                task.SpentMinutes = Math.Max(0, task.SpentMinutes - workLog.SpentMinutes);
                await taskRepository.UpdateAsync(task, cancellationToken);
            }

            await workLogRepository.DeleteAsync(workLog, cancellationToken);
            logger.LogInformation(
                "Deleted local work log {WorkLogId} after Jira worklog {JiraWorklogId} deletion",
                workLog.Id,
                worklogId);
        }

        return Result.Success;
    }

    private static string? ReadWorklogId(JsonElement payload) =>
        JiraWorklogWebhookHandlerBase.ReadWorklogId(payload);
}

public abstract class JiraWorklogWebhookHandlerBase(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger logger)
{
    protected async Task<ErrorOr<Success>> UpsertAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Payload.TryGetProperty("worklog", out var worklogEl))
        {
            return Result.Success;
        }

        var worklogId = ReadWorklogId(context.Payload);
        if (string.IsNullOrWhiteSpace(worklogId))
        {
            return Result.Success;
        }

        var issueKey = worklogEl.TryGetProperty("issueId", out _)
            ? null
            : JiraIssueWebhookShared.ReadIssueIds(context.Payload).Key;
        // Prefer issue from envelope; some payloads only have worklog.issueId (numeric).
        var (envelopeKey, envelopeId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var taskMapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            envelopeKey ?? issueKey,
            envelopeId ?? (worklogEl.TryGetProperty("issueId", out var issueIdEl)
                ? (issueIdEl.ValueKind == JsonValueKind.Number ? issueIdEl.GetRawText() : issueIdEl.GetString())
                : null),
            cancellationToken);
        if (taskMapping is null)
        {
            return Result.Success;
        }

        var startedAt = ParseJiraDate(
            worklogEl.TryGetProperty("started", out var startedEl) ? startedEl.GetString() : null)
            ?? DateTime.UtcNow;
        var seconds = worklogEl.TryGetProperty("timeSpentSeconds", out var secEl)
                      && secEl.TryGetInt32(out var s)
            ? s
            : 60;
        var spentMinutes = Math.Max(1, seconds / 60);
        var endedAt = startedAt.AddMinutes(spentMinutes);
        var comment = worklogEl.TryGetProperty("comment", out var commentEl)
            ? (commentEl.ValueKind == JsonValueKind.String ? commentEl.GetString() : commentEl.ToString())
            : null;

        string? authorEmail = null;
        string? authorName = null;
        if (worklogEl.TryGetProperty("author", out var authorEl)
            && authorEl.ValueKind == JsonValueKind.Object)
        {
            authorEmail = authorEl.TryGetProperty("emailAddress", out var emailEl)
                ? emailEl.GetString()
                : null;
            authorName = authorEl.TryGetProperty("displayName", out var nameEl)
                ? nameEl.GetString()
                : null;
        }

        var members = await projectUserRepository.GetAllAsync(
            filter: m => m.ProjectId == context.Connection.ProjectId,
            include: q => q.Include(m => m.User),
            cancellationToken: cancellationToken);
        var userIdByEmail = members
            .Where(m => m.User is not null && !string.IsNullOrWhiteSpace(m.User.Email))
            .GroupBy(m => m.User.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().UserId, StringComparer.OrdinalIgnoreCase);
        var userIdByFullName = members
            .Where(m => m.User is not null)
            .Select(m => new
            {
                m.UserId,
                FullName = $"{m.User.FirstName} {m.User.LastName}".Trim(),
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.FullName))
            .GroupBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().UserId, StringComparer.OrdinalIgnoreCase);

        var userId = JiraWebhookSyncHelper.ResolveLocalUserId(
                         authorEmail,
                         authorName,
                         userIdByEmail,
                         userIdByFullName)
                     ?? context.Connection.ConnectedByUserId
                     ?? currentUser.GetUserId()
                     ?? members.FirstOrDefault()?.UserId;
        if (userId is null)
        {
            return Result.Success;
        }

        var existingMapping = await mappingRepository.GetAsync(
            filter: m =>
                m.IntegrationConnectionId == context.Connection.Id
                && m.LocalEntityType == "TaskWorkLog"
                && m.ExternalEntityId == worklogId,
            cancellationToken: cancellationToken);

        var task = await taskRepository.GetAsync(
            filter: t => t.Id == taskMapping.LocalEntityId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Result.Success;
        }

        if (existingMapping is not null)
        {
            var existing = await workLogRepository.GetAsync(
                filter: w => w.Id == existingMapping.LocalEntityId,
                cancellationToken: cancellationToken);
            if (existing is not null)
            {
                var delta = spentMinutes - existing.SpentMinutes;
                existing.StartedAt = DateTime.SpecifyKind(startedAt, DateTimeKind.Utc);
                existing.EndedAt = DateTime.SpecifyKind(endedAt, DateTimeKind.Utc);
                existing.SpentMinutes = spentMinutes;
                existing.Comment = string.IsNullOrWhiteSpace(comment) ? existing.Comment : comment.Trim();
                existing.UserId = userId.Value;
                await workLogRepository.UpdateAsync(existing, cancellationToken);
                task.SpentMinutes = Math.Max(0, task.SpentMinutes + delta);
                await taskRepository.UpdateAsync(task, cancellationToken);
                existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
                await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
                return Result.Success;
            }
        }

        var created = new ProjectTaskWorkLog
        {
            Id = Guid.NewGuid(),
            TenantId = context.Connection.TenantId,
            ProjectId = context.Connection.ProjectId,
            TaskId = task.Id,
            UserId = userId.Value,
            StartedAt = DateTime.SpecifyKind(startedAt, DateTimeKind.Utc),
            EndedAt = DateTime.SpecifyKind(endedAt, DateTimeKind.Utc),
            SpentMinutes = spentMinutes,
            Comment = string.IsNullOrWhiteSpace(comment) ? $"Imported from Jira {envelopeKey}" : comment.Trim(),
        };
        await workLogRepository.AddAsync(created, cancellationToken);
        task.SpentMinutes += spentMinutes;
        await taskRepository.UpdateAsync(task, cancellationToken);
        await mappingRepository.AddAsync(
            new IntegrationMapping
            {
                Id = Guid.NewGuid(),
                TenantId = context.Connection.TenantId,
                ProjectId = context.Connection.ProjectId,
                IntegrationConnectionId = context.Connection.Id,
                LocalEntityType = "TaskWorkLog",
                LocalEntityId = created.Id,
                ExternalEntityType = "Worklog",
                ExternalEntityId = worklogId,
                ExternalEntityKey = $"{envelopeKey ?? envelopeId}:{worklogId}",
                LastSyncedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        logger.LogInformation(
            "Imported Jira worklog {WorklogId} onto task {TaskCode}",
            worklogId,
            task.Code);
        return Result.Success;
    }

    internal static string? ReadWorklogId(JsonElement payload)
    {
        if (payload.TryGetProperty("worklog", out var worklogEl)
            && worklogEl.TryGetProperty("id", out var idEl))
        {
            return idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        }

        return null;
    }

    private static DateTime? ParseJiraDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, out var dto) ? dto.UtcDateTime : null;
    }
}
