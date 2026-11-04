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
    IJiraTaskImportService importService,
    ILogger<JiraIssueCreatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.IssueCreated;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var lookup = JiraIssueWebhookShared.ResolveIssueLookup(context.Payload);
        if (lookup is null)
        {
            return Result.Success;
        }

        var result = await importService.UpsertIssueFromWebhookAsync(
            context.Connection,
            context.AccessToken,
            lookup,
            cancellationToken);
        if (result.IsError)
        {
            logger.LogWarning(
                "issue_created upsert failed for {Issue}: {Error}",
                lookup,
                result.FirstError.Description);
        }

        return Result.Success;
    }
}

public sealed class JiraIssueUpdatedWebhookHandler(
    IJiraTaskImportService importService,
    ILogger<JiraIssueUpdatedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.IssueUpdated;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        // Create if missing, otherwise refresh mapped task.
        var lookup = JiraIssueWebhookShared.ResolveIssueLookup(context.Payload);
        if (lookup is null)
        {
            return Result.Success;
        }

        var result = await importService.UpsertIssueFromWebhookAsync(
            context.Connection,
            context.AccessToken,
            lookup,
            cancellationToken);
        if (result.IsError)
        {
            logger.LogWarning(
                "issue_updated upsert failed for {Issue}: {Error}",
                lookup,
                result.FirstError.Description);
        }

        return Result.Success;
    }
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
                m.ProjectIntegrationId == context.Connection.Id
                && (m.LocalEntityId == mapping.LocalEntityId
                    || m.TaskId == mapping.LocalEntityId
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
    public static string? ResolveIssueLookup(JsonElement payload)
    {
        var (key, id) = ReadIssueIds(payload);
        return !string.IsNullOrWhiteSpace(key) ? key : id;
    }

    public static (string? Key, string? Id) ReadIssueIds(JsonElement payload)
    {
        string? key = null;
        string? id = null;
        if (payload.TryGetProperty("issue", out var issueEl))
        {
            key = issueEl.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
            id = issueEl.TryGetProperty("id", out var idEl)
                ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString())
                : null;
        }

        return (key, id);
    }
}
