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
        CreateAsync(context, cancellationToken);
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
        UpdateAsync(context, cancellationToken);
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
        var worklogId = JiraWorklogWebhookHandlerBase.ReadWorklogId(context.Payload);
        if (string.IsNullOrWhiteSpace(worklogId))
        {
            return Result.Success;
        }

        var mapping = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == context.Connection.Id
                && m.LocalEntityType == IntegrationLocalEntityTypes.TaskWorkLog
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
    protected async Task<ErrorOr<Success>> CreateAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorklog(context.Payload, out var worklogId, out var worklogEl)
            || string.IsNullOrWhiteSpace(worklogId))
        {
            return Result.Success;
        }

        var taskMapping = await FindTaskMappingAsync(context, worklogEl, cancellationToken);
        if (taskMapping is null)
        {
            return Result.Success;
        }

        var existingMapping = await FindWorklogMappingAsync(context.Connection.Id, worklogId, cancellationToken);
        if (existingMapping is not null)
        {
            return await UpdateExistingAsync(context, existingMapping, worklogEl, taskMapping, cancellationToken);
        }

        var task = await taskRepository.GetAsync(
            filter: t => t.Id == taskMapping.LocalEntityId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Result.Success;
        }

        var parsed = ParseWorklogFields(worklogEl);
        var userId = await ResolveUserIdAsync(context, worklogEl, cancellationToken);
        if (userId is null)
        {
            return Result.Success;
        }

        var (envelopeKey, envelopeId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var created = new ProjectTaskWorkLog
        {
            Id = Guid.NewGuid(),
            TenantId = context.Connection.TenantId,
            ProjectId = context.Connection.ProjectId,
            TaskId = task.Id,
            UserId = userId.Value,
            StartedAt = DateTime.SpecifyKind(parsed.StartedAt, DateTimeKind.Utc),
            EndedAt = DateTime.SpecifyKind(parsed.EndedAt, DateTimeKind.Utc),
            SpentMinutes = parsed.SpentMinutes,
            Comment = string.IsNullOrWhiteSpace(parsed.Comment)
                ? $"Imported from Jira {envelopeKey}"
                : parsed.Comment.Trim(),
        };
        await workLogRepository.AddAsync(created, cancellationToken);
        task.SpentMinutes += parsed.SpentMinutes;
        await taskRepository.UpdateAsync(task, cancellationToken);
        await mappingRepository.AddAsync(
            IntegrationMappingFactory.ForWorkLog(
                context.Connection.TenantId,
                context.Connection.ProjectId,
                context.Connection.Id,
                created.Id,
                worklogId,
                $"{envelopeKey ?? envelopeId}:{worklogId}"),
            cancellationToken);

        logger.LogInformation(
            "Imported Jira worklog {WorklogId} onto task {TaskCode}",
            worklogId,
            task.Code);
        return Result.Success;
    }

    protected async Task<ErrorOr<Success>> UpdateAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadWorklog(context.Payload, out var worklogId, out var worklogEl)
            || string.IsNullOrWhiteSpace(worklogId))
        {
            return Result.Success;
        }

        var existingMapping = await FindWorklogMappingAsync(context.Connection.Id, worklogId, cancellationToken);
        if (existingMapping is null)
        {
            return Result.Success;
        }

        var taskMapping = await FindTaskMappingAsync(context, worklogEl, cancellationToken);
        if (taskMapping is null)
        {
            return Result.Success;
        }

        return await UpdateExistingAsync(context, existingMapping, worklogEl, taskMapping, cancellationToken);
    }

    private async Task<ErrorOr<Success>> UpdateExistingAsync(
        JiraWebhookEventContext context,
        IntegrationMapping existingMapping,
        JsonElement worklogEl,
        IntegrationMapping taskMapping,
        CancellationToken cancellationToken)
    {
        var existing = await workLogRepository.GetAsync(
            filter: w => w.Id == existingMapping.LocalEntityId,
            cancellationToken: cancellationToken);
        if (existing is null)
        {
            return Result.Success;
        }

        var task = await taskRepository.GetAsync(
            filter: t => t.Id == taskMapping.LocalEntityId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Result.Success;
        }

        var parsed = ParseWorklogFields(worklogEl);
        var userId = await ResolveUserIdAsync(context, worklogEl, cancellationToken) ?? existing.UserId;
        var delta = parsed.SpentMinutes - existing.SpentMinutes;
        existing.StartedAt = DateTime.SpecifyKind(parsed.StartedAt, DateTimeKind.Utc);
        existing.EndedAt = DateTime.SpecifyKind(parsed.EndedAt, DateTimeKind.Utc);
        existing.SpentMinutes = parsed.SpentMinutes;
        existing.Comment = string.IsNullOrWhiteSpace(parsed.Comment) ? existing.Comment : parsed.Comment.Trim();
        existing.UserId = userId;
        await workLogRepository.UpdateAsync(existing, cancellationToken);
        task.SpentMinutes = Math.Max(0, task.SpentMinutes + delta);
        await taskRepository.UpdateAsync(task, cancellationToken);
        existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
        JiraMappingTouch.MarkActive(existingMapping);
        await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
        return Result.Success;
    }

    private async Task<IntegrationMapping?> FindTaskMappingAsync(
        JiraWebhookEventContext context,
        JsonElement worklogEl,
        CancellationToken cancellationToken)
    {
        var (envelopeKey, envelopeId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var worklogIssueId = worklogEl.TryGetProperty("issueId", out var issueIdEl)
            ? (issueIdEl.ValueKind == JsonValueKind.Number ? issueIdEl.GetRawText() : issueIdEl.GetString())
            : null;
        return await syncHelper.FindTaskMappingAsync(
            context.Connection,
            envelopeKey,
            envelopeId ?? worklogIssueId,
            cancellationToken);
    }

    private async Task<IntegrationMapping?> FindWorklogMappingAsync(
        Guid connectionId,
        string worklogId,
        CancellationToken cancellationToken) =>
        await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == connectionId
                && m.LocalEntityType == IntegrationLocalEntityTypes.TaskWorkLog
                && m.ExternalEntityId == worklogId,
            cancellationToken: cancellationToken);

    private async Task<Guid?> ResolveUserIdAsync(
        JiraWebhookEventContext context,
        JsonElement worklogEl,
        CancellationToken cancellationToken)
    {
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

        return JiraWebhookSyncHelper.ResolveLocalUserId(
                   authorEmail,
                   authorName,
                   userIdByEmail,
                   userIdByFullName)
               ?? context.Connection.ConnectedByUserId
               ?? currentUser.GetUserId()
               ?? members.FirstOrDefault()?.UserId;
    }

    internal static string? ReadWorklogId(JsonElement payload) =>
        TryReadWorklog(payload, out var id, out _) ? id : null;

    private static bool TryReadWorklog(JsonElement payload, out string? worklogId, out JsonElement worklogEl)
    {
        worklogId = null;
        worklogEl = default;
        if (!payload.TryGetProperty("worklog", out worklogEl)
            || !worklogEl.TryGetProperty("id", out var idEl))
        {
            return false;
        }

        worklogId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        return !string.IsNullOrWhiteSpace(worklogId);
    }

    private static (DateTime StartedAt, DateTime EndedAt, int SpentMinutes, string? Comment) ParseWorklogFields(
        JsonElement worklogEl)
    {
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
        return (startedAt, endedAt, spentMinutes, comment);
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
