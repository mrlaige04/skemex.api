using ErrorOr;
using Microsoft.Extensions.Logging;
using Skemex.Application.Services.Integrations.Jira.Webhooks;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

/// <summary>
/// Attachment file sync needs blob download plumbing; acknowledge events for now when enabled.
/// </summary>
public sealed class JiraAttachmentCreatedWebhookHandler(ILogger<JiraAttachmentCreatedWebhookHandler> logger)
    : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.AttachmentCreated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Jira attachment created webhook received for connection {ConnectionId} (file sync not implemented yet)",
            context.Connection.Id);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}

public sealed class JiraAttachmentDeletedWebhookHandler(ILogger<JiraAttachmentDeletedWebhookHandler> logger)
    : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.AttachmentDeleted;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Jira attachment deleted webhook received for connection {ConnectionId} (file sync not implemented yet)",
            context.Connection.Id);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}

/// <summary>
/// User lifecycle events are not reliably delivered on project-scoped Jira webhooks.
/// Handlers exist so settings/API stay complete; processing is a no-op.
/// </summary>
public sealed class JiraUserCreatedWebhookHandler(ILogger<JiraUserCreatedWebhookHandler> logger)
    : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserCreated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Ignoring Jira user.created for connection {ConnectionId}", context.Connection.Id);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}

public sealed class JiraUserUpdatedWebhookHandler(ILogger<JiraUserUpdatedWebhookHandler> logger)
    : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserUpdated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Ignoring Jira user.updated for connection {ConnectionId}", context.Connection.Id);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}

public sealed class JiraUserDeletedWebhookHandler(ILogger<JiraUserDeletedWebhookHandler> logger)
    : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.UserDeleted;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Ignoring Jira user.deleted for connection {ConnectionId}", context.Connection.Id);
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
