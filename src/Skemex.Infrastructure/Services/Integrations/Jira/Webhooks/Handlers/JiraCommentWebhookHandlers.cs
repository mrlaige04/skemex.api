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
using Skemex.Infrastructure.Services.Integrations.Jira.Webhooks;

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
        CreateAsync(context, cancellationToken);
}

public abstract class JiraCommentWebhookHandlerBase(
    JiraWebhookSyncHelper syncHelper,
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ICurrentUser currentUser,
    ILogger logger)
{
    protected async Task<ErrorOr<Success>> CreateAsync(
        JiraWebhookEventContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadComment(context.Payload, out var commentId, out var commentEl)
            || string.IsNullOrWhiteSpace(commentId))
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

        var existingMapping = await FindCommentMappingAsync(context.Connection.Id, commentId, cancellationToken);
        if (existingMapping is not null)
        {
            return await UpdateExistingAsync(existingMapping, commentEl, cancellationToken);
        }

        var html = ReadCommentHtml(commentEl);
        if (string.IsNullOrWhiteSpace(html))
        {
            return Result.Success;
        }

        var authorId = await ResolveAuthorIdAsync(context, commentEl, cancellationToken);
        if (authorId is null)
        {
            logger.LogWarning("Skipping Jira comment {CommentId}: no author could be resolved", commentId);
            return Result.Success;
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
            IntegrationMappingFactory.ForComment(
                context.Connection.TenantId,
                context.Connection.ProjectId,
                context.Connection.Id,
                created.Id,
                commentId,
                $"{issueKey ?? issueId}:{commentId}"),
            cancellationToken);

        logger.LogInformation(
            "Imported Jira comment {CommentId} onto task {TaskId}",
            commentId,
            taskMapping.LocalEntityId);
        return Result.Success;
    }

    private async Task<ErrorOr<Success>> UpdateExistingAsync(
        IntegrationMapping existingMapping,
        JsonElement commentEl,
        CancellationToken cancellationToken)
    {
        var existing = await commentRepository.GetAsync(
            filter: c => c.Id == existingMapping.LocalEntityId,
            cancellationToken: cancellationToken);
        if (existing is null)
        {
            return Result.Success;
        }

        var html = ReadCommentHtml(commentEl);
        if (string.IsNullOrWhiteSpace(html))
        {
            return Result.Success;
        }

        existing.Body = html;
        await commentRepository.UpdateAsync(existing, cancellationToken);
        existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
        JiraMappingTouch.MarkActive(existingMapping);
        await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
        return Result.Success;
    }

    private async Task<IntegrationMapping?> FindCommentMappingAsync(
        Guid connectionId,
        string commentId,
        CancellationToken cancellationToken) =>
        await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectIntegrationId == connectionId
                && m.LocalEntityType == IntegrationLocalEntityTypes.TaskComment
                && m.ExternalEntityId == commentId,
            cancellationToken: cancellationToken);

    private async Task<Guid?> ResolveAuthorIdAsync(
        JiraWebhookEventContext context,
        JsonElement commentEl,
        CancellationToken cancellationToken)
    {
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

        return JiraWebhookSyncHelper.ResolveLocalUserId(
                   authorEmail,
                   authorName,
                   userIdByEmail,
                   userIdByFullName)
               ?? context.Connection.ConnectedByUserId
               ?? currentUser.GetUserId()
               ?? members.FirstOrDefault()?.UserId;
    }

    private static bool TryReadComment(JsonElement payload, out string? commentId, out JsonElement commentEl)
    {
        commentId = null;
        commentEl = default;
        if (!payload.TryGetProperty("comment", out commentEl))
        {
            return false;
        }

        if (!commentEl.TryGetProperty("id", out var idEl))
        {
            return false;
        }

        commentId = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        return !string.IsNullOrWhiteSpace(commentId);
    }

    private static string ReadCommentHtml(JsonElement commentEl)
    {
        string? bodyText = null;
        if (commentEl.TryGetProperty("body", out var bodyEl))
        {
            bodyText = bodyEl.ValueKind == JsonValueKind.String
                ? bodyEl.GetString()
                : bodyEl.ToString();
        }

        var html = PlainTextToRichHtml(bodyText);
        if (html.Length > ProjectTaskCommentDtoHelper.MaxBodyLength)
        {
            html = html[..ProjectTaskCommentDtoHelper.MaxBodyLength];
        }

        return html;
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
