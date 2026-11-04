using Microsoft.EntityFrameworkCore;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira.Webhooks;

public sealed class JiraWebhookSyncHelper(
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<ProjectColumn> columnRepository,
    ITenantRepository<ProjectUser> projectUserRepository)
{
    public async Task<IntegrationMapping?> FindTaskMappingAsync(
        ProjectIntegration connection,
        string? issueKey,
        string? issueId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(issueKey))
        {
            var byKey = await mappingRepository.GetAsync(
                filter: m =>
                    m.ProjectIntegrationId == connection.Id
                    && m.LocalEntityType == IntegrationLocalEntityTypes.Task
                    && m.ExternalEntityKey == issueKey,
                cancellationToken: cancellationToken);
            if (byKey is not null)
            {
                return byKey;
            }
        }

        if (!string.IsNullOrWhiteSpace(issueId))
        {
            return await mappingRepository.GetAsync(
                filter: m =>
                    m.ProjectIntegrationId == connection.Id
                    && m.LocalEntityType == IntegrationLocalEntityTypes.Task
                    && m.ExternalEntityId == issueId,
                cancellationToken: cancellationToken);
        }

        return null;
    }

    public async Task ApplyRemoteIssueToTaskAsync(
        ProjectIntegration connection,
        ProjectTask task,
        JiraRemoteIssueDto remote,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(remote.Summary) ? task.Title : remote.Summary.Trim();
        task.Title = title.Length > 500 ? title[..500] : title;
        if (!string.IsNullOrWhiteSpace(remote.Description))
        {
            task.Description = remote.Description.Trim();
        }

        task.Tags = remote.Labels.Take(50).ToList();
        task.OriginalEstimateMinutes = remote.OriginalEstimateMinutes ?? task.OriginalEstimateMinutes;
        task.RemainingEstimateMinutes =
            remote.RemainingEstimateMinutes ?? task.RemainingEstimateMinutes;
        if (remote.TimeSpentMinutes is { } spent)
        {
            task.SpentMinutes = spent;
        }

        var members = await projectUserRepository.GetAllAsync(
            filter: m => m.ProjectId == connection.ProjectId,
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

        if (remote.AssigneeEmail is null && remote.AssigneeDisplayName is null)
        {
            task.AssigneeId = null;
        }
        else
        {
            var assigneeId = ResolveLocalUserId(
                remote.AssigneeEmail,
                remote.AssigneeDisplayName,
                userIdByEmail,
                userIdByFullName);
            if (assigneeId is not null)
            {
                task.AssigneeId = assigneeId;
            }
        }

        if (!string.IsNullOrWhiteSpace(remote.IssueType))
        {
            task.Type = MapIssueType(remote.IssueType, task.ParentId is not null);
        }

        var columns = await columnRepository.GetAllAsync(
            filter: c => c.ProjectId == connection.ProjectId,
            cancellationToken: cancellationToken);
        var columnId = ResolveColumnId(remote.Status, columns);
        if (columnId is not null)
        {
            task.ProjectColumnId = columnId.Value;
        }

        await taskRepository.UpdateAsync(task, cancellationToken);
    }

    public static Guid? ResolveLocalUserId(
        string? email,
        string? fullName,
        IReadOnlyDictionary<string, Guid> userIdByEmail,
        IReadOnlyDictionary<string, Guid> userIdByFullName)
    {
        if (!string.IsNullOrWhiteSpace(email)
            && userIdByEmail.TryGetValue(email, out var byEmail))
        {
            return byEmail;
        }

        if (!string.IsNullOrWhiteSpace(fullName)
            && userIdByFullName.TryGetValue(fullName.Trim(), out var byName))
        {
            return byName;
        }

        return null;
    }

    private static Guid? ResolveColumnId(string? jiraStatus, IReadOnlyList<ProjectColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(jiraStatus) || columns.Count == 0)
        {
            return null;
        }

        var normalized = NormalizeStatus(jiraStatus);
        foreach (var column in columns)
        {
            if (NormalizeStatus(column.Title) == normalized
                || NormalizeStatus(column.Key) == normalized
                || NormalizeStatus(column.Key.Replace('-', ' ')) == normalized)
            {
                return column.Id;
            }
        }

        var aliasKey = StatusAliasToColumnKey(normalized);
        if (aliasKey is null)
        {
            return null;
        }

        return columns
            .FirstOrDefault(c => string.Equals(c.Key, aliasKey, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    private static string? StatusAliasToColumnKey(string normalizedStatus) =>
        normalizedStatus switch
        {
            "backlog" => "backlog",
            "to do" or "todo" or "open" or "new" => "todo",
            "in progress" or "inprogress" or "progress" or "doing" => "in-progress",
            "in qa" or "inqa" or "qa" or "testing" or "in review" or "review" => "in-qa",
            "done" or "closed" or "resolved" or "complete" or "completed" => "done",
            _ => null,
        };

    private static string NormalizeStatus(string value) =>
        string.Join(
            ' ',
            value.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static ProjectTaskType MapIssueType(string issueType, bool hasParent)
    {
        if (hasParent)
        {
            return ProjectTaskType.Task;
        }

        if (string.Equals(issueType, "Bug", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectTaskType.Bug;
        }

        if (string.Equals(issueType, "Story", StringComparison.OrdinalIgnoreCase)
            || string.Equals(issueType, "Epic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(issueType, "Feature", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectTaskType.Feature;
        }

        return ProjectTaskType.Task;
    }
}
