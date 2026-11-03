using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

public sealed class JiraIssueCreatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IJiraApiClient jiraApiClient,
    IJiraTaskImportService importService,
    ILogger<JiraIssueCreatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.IssueCreated;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var (issueKey, issueId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var mapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            issueKey,
            issueId,
            cancellationToken);

        // Already linked → refresh fields; otherwise create a local task.
        if (mapping is not null)
        {
            return await JiraIssueWebhookShared.SyncMappedIssueAsync(
                context,
                syncHelper,
                taskRepository,
                mappingRepository,
                jiraApiClient,
                logger,
                cancellationToken);
        }

        var lookup = !string.IsNullOrWhiteSpace(issueKey) ? issueKey! : issueId;
        if (string.IsNullOrWhiteSpace(lookup))
        {
            logger.LogDebug("Ignoring issue_created webhook without issue key/id");
            return Result.Success;
        }

        var upsert = await importService.UpsertIssueFromWebhookAsync(
            context.Connection,
            context.AccessToken,
            lookup,
            cancellationToken);
        if (upsert.IsError)
        {
            logger.LogWarning(
                "Failed to create local task from Jira {Issue}: {Error}",
                lookup,
                upsert.FirstError.Description);
            // Ack to Jira; avoid poison retries for validation issues.
            return Result.Success;
        }

        return Result.Success;
    }
}

public sealed class JiraIssueUpdatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    IJiraApiClient jiraApiClient,
    ILogger<JiraIssueUpdatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.IssueUpdated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        JiraIssueWebhookShared.SyncMappedIssueAsync(
            context,
            syncHelper,
            taskRepository,
            mappingRepository,
            jiraApiClient,
            logger,
            cancellationToken);
}

public sealed class JiraIssueDeletedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ILogger<JiraIssueDeletedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.IssueDeleted;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var (issueKey, issueId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var mapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            issueKey,
            issueId,
            cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var task = await taskRepository.GetAsync(
            filter: t => t.Id == mapping.LocalEntityId && t.ProjectId == context.Connection.ProjectId,
            cancellationToken: cancellationToken);

        var related = await mappingRepository.GetAllAsync(
            filter: m =>
                m.IntegrationConnectionId == context.Connection.Id
                && (m.LocalEntityId == mapping.LocalEntityId
                    || (issueKey != null
                        && m.ExternalEntityKey != null
                        && m.ExternalEntityKey.StartsWith(issueKey + ":"))),
            cancellationToken: cancellationToken);

        foreach (var row in related)
        {
            await mappingRepository.DeleteAsync(row, cancellationToken);
        }

        if (task is not null)
        {
            await taskRepository.DeleteAsync(task, cancellationToken);
            logger.LogInformation(
                "Deleted local task {TaskCode} after Jira issue {IssueKey} deletion",
                task.Code,
                issueKey ?? issueId);
        }

        return Result.Success;
    }
}

internal static class JiraIssueWebhookShared
{
    public static async Task<ErrorOr<Success>> SyncMappedIssueAsync(
        JiraWebhookEventContext context,
        JiraWebhookSyncHelper syncHelper,
        ITenantRepository<ProjectTask> taskRepository,
        ITenantRepository<IntegrationMapping> mappingRepository,
        IJiraApiClient jiraApiClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var (issueKey, issueId) = ReadIssueIds(context.Payload);
        var mapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            issueKey,
            issueId,
            cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var lookup = !string.IsNullOrWhiteSpace(issueKey) ? issueKey! : issueId!;
        var remote = await jiraApiClient.GetRemoteIssueAsync(
            context.AccessToken,
            context.Connection.CloudId!,
            lookup,
            cancellationToken);
        if (remote is null)
        {
            return Result.Success;
        }

        var task = await taskRepository.GetAsync(
            filter: t => t.Id == mapping.LocalEntityId && t.ProjectId == context.Connection.ProjectId,
            cancellationToken: cancellationToken);
        if (task is null)
        {
            return Result.Success;
        }

        await syncHelper.ApplyRemoteIssueToTaskAsync(
            context.Connection,
            task,
            remote,
            cancellationToken);

        mapping.ExternalEntityId = remote.Id;
        mapping.ExternalEntityKey = remote.Key;
        mapping.LastSyncedAt = DateTimeOffset.UtcNow;
        await mappingRepository.UpdateAsync(mapping, cancellationToken);

        logger.LogInformation(
            "Webhook {Event} synced Jira {IssueKey} → {TaskCode}",
            context.EventKey,
            remote.Key,
            task.Code);

        return Result.Success;
    }

    public static (string? Key, string? Id) ReadIssueIds(JsonElement payload)
    {
        string? key = null;
        string? id = null;
        if (payload.TryGetProperty("issue", out var issueEl))
        {
            key = issueEl.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
            id = issueEl.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        }

        return (key, id);
    }
}
