using System.Net;
using System.Text;
using System.Text.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks.Handlers;

public sealed class JiraCommentCreatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger<JiraCommentCreatedWebhookHandler> logger)
    : JiraCommentWebhookHandlerBase(
        syncHelper,
        commentRepository,
        mappingRepository,
        projectUserRepository,
        currentUser,
        logger),
      IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.CommentCreated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(context, cancellationToken);
}

public sealed class JiraCommentUpdatedWebhookHandler(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger<JiraCommentUpdatedWebhookHandler> logger)
    : JiraCommentWebhookHandlerBase(
        syncHelper,
        commentRepository,
        mappingRepository,
        projectUserRepository,
        currentUser,
        logger),
      IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.CommentUpdated;

    public Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(context, cancellationToken);
}

public sealed class JiraCommentDeletedWebhookHandler(
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ILogger<JiraCommentDeletedWebhookHandler> logger) : IJiraWebhookEventHandler
{
    public string EventKey => JiraWebhookEventCatalog.CommentDeleted;

    public async Task<ErrorOr<Success>> HandleAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken = default)
    {
        var commentId = ReadCommentId(context.Payload);
        if (string.IsNullOrWhiteSpace(commentId))
        {
            return Result.Success;
        }

        var mapping = await mappingRepository.GetAsync(
            filter: m =>
                m.IntegrationConnectionId == context.Connection.Id
                && m.LocalEntityType == "TaskComment"
                && m.ExternalEntityId == commentId,
            cancellationToken: cancellationToken);
        if (mapping is null)
        {
            return Result.Success;
        }

        var comment = await commentRepository.GetAsync(
            filter: c => c.Id == mapping.LocalEntityId,
            cancellationToken: cancellationToken);
        await mappingRepository.DeleteAsync(mapping, cancellationToken);
        if (comment is not null)
        {
            await commentRepository.DeleteAsync(comment, cancellationToken);
            logger.LogInformation(
                "Deleted local comment {CommentId} after Jira comment {JiraCommentId} deletion",
                comment.Id,
                commentId);
        }

        return Result.Success;
    }

    private static string? ReadCommentId(JsonElement payload)
    {
        if (payload.TryGetProperty("comment", out var commentEl)
            && commentEl.TryGetProperty("id", out var idEl))
        {
            return idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        }

        return null;
    }
}

public abstract class JiraCommentWebhookHandlerBase(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger logger)
{
    protected async Task<ErrorOr<Success>> UpsertAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Payload.TryGetProperty("comment", out var commentEl))
        {
            return Result.Success;
        }

        var commentId = commentEl.TryGetProperty("id", out var idEl)
            ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString())
            : null;
        if (string.IsNullOrWhiteSpace(commentId))
        {
            return Result.Success;
        }

        var (issueKey, issueId) = JiraIssueWebhookShared.ReadIssueIds(context.Payload);
        var taskMapping = await syncHelper.FindTaskMappingAsync(
            context.Connection,
            issueKey,
            issueId,
            cancellationToken);
        if (taskMapping is null)
        {
            return Result.Success;
        }

        string? bodyText = null;
        if (commentEl.TryGetProperty("body", out var bodyEl))
        {
            bodyText = bodyEl.ValueKind == JsonValueKind.String
                ? bodyEl.GetString()
                : bodyEl.ToString();
        }

        var html = PlainTextToRichHtml(bodyText);
        if (string.IsNullOrWhiteSpace(html))
        {
            return Result.Success;
        }

        if (html.Length > ProjectTaskCommentDtoHelper.MaxBodyLength)
        {
            html = html[..ProjectTaskCommentDtoHelper.MaxBodyLength];
        }

        string? authorEmail = null;
        string? authorName = null;
        if (commentEl.TryGetProperty("author", out var authorEl)
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

        var authorId = JiraWebhookSyncHelper.ResolveLocalUserId(
                          authorEmail,
                          authorName,
                          userIdByEmail,
                          userIdByFullName)
                      ?? context.Connection.ConnectedByUserId
                      ?? currentUser.GetUserId()
                      ?? members.FirstOrDefault()?.UserId;
        if (authorId is null)
        {
            logger.LogWarning("Skipping Jira comment {CommentId}: no author could be resolved", commentId);
            return Result.Success;
        }

        var existingMapping = await mappingRepository.GetAsync(
            filter: m =>
                m.IntegrationConnectionId == context.Connection.Id
                && m.LocalEntityType == "TaskComment"
                && m.ExternalEntityId == commentId,
            cancellationToken: cancellationToken);

        if (existingMapping is not null)
        {
            var existing = await commentRepository.GetAsync(
                filter: c => c.Id == existingMapping.LocalEntityId,
                cancellationToken: cancellationToken);
            if (existing is not null)
            {
                existing.Body = html;
                await commentRepository.UpdateAsync(existing, cancellationToken);
                existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
                await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
                return Result.Success;
            }
        }

        var created = new ProjectTaskComment
        {
            Id = Guid.NewGuid(),
            TenantId = context.Connection.TenantId,
            ProjectId = context.Connection.ProjectId,
            TaskId = taskMapping.LocalEntityId,
            AuthorId = authorId.Value,
            Body = html,
        };
        await commentRepository.AddAsync(created, cancellationToken);
        await mappingRepository.AddAsync(
            new IntegrationMapping
            {
                Id = Guid.NewGuid(),
                TenantId = context.Connection.TenantId,
                ProjectId = context.Connection.ProjectId,
                IntegrationConnectionId = context.Connection.Id,
                LocalEntityType = "TaskComment",
                LocalEntityId = created.Id,
                ExternalEntityType = "Comment",
                ExternalEntityId = commentId,
                ExternalEntityKey = $"{issueKey ?? issueId}:{commentId}",
                LastSyncedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        logger.LogInformation(
            "Imported Jira comment {CommentId} onto task {TaskId}",
            commentId,
            taskMapping.LocalEntityId);
        return Result.Success;
    }

    private static string PlainTextToRichHtml(string? plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
        {
            return string.Empty;
        }

        var normalized = plain.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var blocks = normalized.Split(
            "\n\n",
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (blocks.Length == 0)
        {
            blocks = [normalized];
        }

        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            var encoded = WebUtility.HtmlEncode(block).Replace("\n", "<br>", StringComparison.Ordinal);
            sb.Append("<p>").Append(encoded).Append("</p>");
        }

        return sb.ToString();
    }
}
