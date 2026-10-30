using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed partial class JiraTaskExportService(
    ITenantRepository<IntegrationConnection> connectionRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectTask> taskRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient,
    ILogger<JiraTaskExportService> logger) : IJiraTaskExportService
{
    public const int MaxExportTasks = 200;

    public async Task<ErrorOr<ExportTasksToJiraResultDto>> ExportAsync(
        Guid projectId,
        ExportTasksToJiraRequest request,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionRepository.GetAsync(
            filter: c => c.ProjectId == projectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (connection is null
            || connection.Status != IntegrationConnectionStatus.Connected
            || string.IsNullOrWhiteSpace(connection.CloudId)
            || string.IsNullOrWhiteSpace(connection.ExternalProjectKey))
        {
            return Error.Validation(
                "Jira.NotConnected",
                "Connect Jira and map a project before exporting tasks.");
        }

        var filter = BuildTaskFilter(projectId, request);
        var tasks = await taskRepository.GetAllAsync(
            filter: filter,
            include: query => query
                .Include(t => t.Assignee)
                .Include(t => t.Reporter)
                .Include(t => t.Parent)
                .Include(t => t.WorkLogs)
                .ThenInclude(w => w.User)
                .OrderBy(t => t.ParentId.HasValue ? 1 : 0)
                .ThenBy(t => t.CreatedAt)
                .ThenBy(t => t.Code),
            cancellationToken: cancellationToken);

        if (tasks.Count == 0)
        {
            return new ExportTasksToJiraResultDto();
        }

        if (tasks.Count > MaxExportTasks)
        {
            return Error.Validation(
                "Jira.ExportTooMany",
                $"Export is limited to {MaxExportTasks} tasks. Narrow filters or export a selection.");
        }

        var existingMappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.IntegrationConnectionId == connection.Id
                && m.LocalEntityType == "Task",
            cancellationToken: cancellationToken);

        var mappedByTaskId = existingMappings.ToDictionary(m => m.LocalEntityId);
        var localToJiraKey = existingMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalEntityKey))
            .ToDictionary(m => m.LocalEntityId, m => m.ExternalEntityKey!);

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        var accessToken = tokenResult.Value;
        var cloudId = connection.CloudId!;
        var projectKey = connection.ExternalProjectKey!;
        var accountCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ExportTasksToJiraItemResultDto>(tasks.Count);
        var exported = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.SkipAlreadyExported && mappedByTaskId.TryGetValue(task.Id, out var existing))
            {
                skipped++;
                items.Add(new ExportTasksToJiraItemResultDto
                {
                    TaskId = task.Id,
                    TaskCode = task.Code,
                    Status = "Skipped",
                    JiraIssueKey = existing.ExternalEntityKey,
                    Message = "Already exported to Jira.",
                });
                continue;
            }

            try
            {
                var issue = await ExportOneAsync(
                    task,
                    accessToken,
                    cloudId,
                    projectKey,
                    localToJiraKey,
                    accountCache,
                    cancellationToken);

                if (mappedByTaskId.TryGetValue(task.Id, out var prior))
                {
                    prior.ExternalEntityId = issue.Id;
                    prior.ExternalEntityKey = issue.Key;
                    prior.LastSyncedAt = DateTimeOffset.UtcNow;
                    await mappingRepository.UpdateAsync(prior, cancellationToken);
                }
                else
                {
                    var mapping = new IntegrationMapping
                    {
                        Id = Guid.NewGuid(),
                        TenantId = connection.TenantId,
                        ProjectId = projectId,
                        IntegrationConnectionId = connection.Id,
                        LocalEntityType = "Task",
                        LocalEntityId = task.Id,
                        ExternalEntityType = "Issue",
                        ExternalEntityId = issue.Id,
                        ExternalEntityKey = issue.Key,
                        LastSyncedAt = DateTimeOffset.UtcNow,
                    };
                    await mappingRepository.AddAsync(mapping, cancellationToken);
                    mappedByTaskId[task.Id] = mapping;
                }

                localToJiraKey[task.Id] = issue.Key;
                exported++;
                items.Add(new ExportTasksToJiraItemResultDto
                {
                    TaskId = task.Id,
                    TaskCode = task.Code,
                    Status = "Exported",
                    JiraIssueKey = issue.Key,
                });
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Failed to export task {TaskCode} to Jira", task.Code);
                items.Add(new ExportTasksToJiraItemResultDto
                {
                    TaskId = task.Id,
                    TaskCode = task.Code,
                    Status = "Failed",
                    Message = Truncate(ex.Message, 400),
                });
            }
        }

        return new ExportTasksToJiraResultDto
        {
            TotalCandidates = tasks.Count,
            Exported = exported,
            Skipped = skipped,
            Failed = failed,
            Items = items,
        };
    }

    private async Task<JiraCreatedIssueDto> ExportOneAsync(
        ProjectTask task,
        string accessToken,
        string cloudId,
        string projectKey,
        IReadOnlyDictionary<Guid, string> localToJiraKey,
        Dictionary<string, string?> accountCache,
        CancellationToken cancellationToken)
    {
        string? parentKey = null;
        var issueType = MapIssueType(task.Type, hasParent: false);

        if (task.ParentId is { } parentId
            && localToJiraKey.TryGetValue(parentId, out var mappedParent))
        {
            parentKey = mappedParent;
            issueType = "Sub-task";
        }

        string? assigneeAccountId = null;
        if (task.Assignee is not null)
        {
            var fullName = $"{task.Assignee.FirstName} {task.Assignee.LastName}".Trim();
            assigneeAccountId = await ResolveAccountIdAsync(
                accessToken,
                cloudId,
                task.Assignee.Email,
                fullName,
                accountCache,
                cancellationToken);
        }

        var created = await jiraApiClient.CreateIssueAsync(
            accessToken,
            cloudId,
            new JiraCreateIssueRequest
            {
                ProjectKey = projectKey,
                Summary = BuildSummary(task),
                IssueTypeName = issueType,
                Description = BuildDescription(task),
                AssigneeAccountId = assigneeAccountId,
                ParentIssueKey = parentKey,
                Labels = BuildLabels(task),
                OriginalEstimate = FormatEstimate(task.OriginalEstimateMinutes),
                RemainingEstimate = FormatEstimate(task.RemainingEstimateMinutes),
            },
            cancellationToken);

        foreach (var workLog in task.WorkLogs.OrderBy(w => w.StartedAt))
        {
            if (workLog.SpentMinutes <= 0)
            {
                continue;
            }

            try
            {
                await jiraApiClient.AddWorklogAsync(
                    accessToken,
                    cloudId,
                    created.Key,
                    new JiraAddWorklogRequest
                    {
                        Started = FormatJiraDateTime(workLog.StartedAt),
                        TimeSpentSeconds = workLog.SpentMinutes * 60,
                        Comment = BuildWorklogComment(workLog),
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Worklog export failed for task {TaskCode} / issue {IssueKey}",
                    task.Code,
                    created.Key);
            }
        }

        return created;
    }

    private async Task<string?> ResolveAccountIdAsync(
        string accessToken,
        string cloudId,
        string? email,
        string? fullName,
        Dictionary<string, string?> accountCache,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildAssigneeCacheKey(email, fullName);
        if (accountCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            JiraUserDto? user = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                user = await jiraApiClient.FindUserByEmailAsync(
                    accessToken,
                    cloudId,
                    email,
                    cancellationToken);
            }

            if (user is null && !string.IsNullOrWhiteSpace(fullName))
            {
                user = await jiraApiClient.FindUserByDisplayNameAsync(
                    accessToken,
                    cloudId,
                    fullName,
                    cancellationToken);
            }

            accountCache[cacheKey] = user?.AccountId;
            return user?.AccountId;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to resolve Jira account for {Email} / {FullName}",
                email,
                fullName);
            accountCache[cacheKey] = null;
            return null;
        }
    }

    private static string BuildAssigneeCacheKey(string? email, string? fullName)
    {
        var emailPart = string.IsNullOrWhiteSpace(email) ? "-" : email.Trim().ToLowerInvariant();
        var namePart = string.IsNullOrWhiteSpace(fullName) ? "-" : fullName.Trim().ToLowerInvariant();
        return $"{emailPart}|{namePart}";
    }

    private static System.Linq.Expressions.Expression<Func<ProjectTask, bool>> BuildTaskFilter(
        Guid projectId,
        ExportTasksToJiraRequest request) =>
        request.Scope switch
        {
            JiraExportScope.Selected => ProjectTaskQueryHelper.BuildSelectedFilter(
                projectId,
                (request.SelectedTaskIds ?? [])
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList()),
            JiraExportScope.All => ProjectTaskQueryHelper.BuildFilter(
                projectId,
                columnId: null,
                assigneeId: null,
                unassigned: false,
                search: null),
            _ => ProjectTaskQueryHelper.BuildFilter(
                projectId,
                request.ColumnId,
                request.AssigneeId,
                request.Unassigned,
                request.Search),
        };

    private static string MapIssueType(ProjectTaskType type, bool hasParent) =>
        hasParent
            ? "Sub-task"
            : type switch
            {
                ProjectTaskType.Bug => "Bug",
                ProjectTaskType.Feature => "Story",
                _ => "Task",
            };

    private static string BuildSummary(ProjectTask task)
    {
        var title = string.IsNullOrWhiteSpace(task.Title) ? task.Code : task.Title.Trim();
        var summary = $"[{task.Code}] {title}";
        return summary.Length <= 255 ? summary : summary[..255];
    }

    private static string BuildDescription(ProjectTask task)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Skemex key: {task.Code}");
        sb.AppendLine($"Type: {task.Type}");
        if (task.StoryPoints is { } sp)
        {
            sb.AppendLine($"Story points: {sp.ToString(CultureInfo.InvariantCulture)}");
        }

        if (task.SpentMinutes > 0)
        {
            sb.AppendLine($"Spent (Skemex total): {task.SpentMinutes}m");
        }

        sb.AppendLine();
        var description = HtmlToPlainText(task.Description);
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine(description.Trim());
            sb.AppendLine();
        }

        if (task.AcceptanceCriteria is { Count: > 0 })
        {
            sb.AppendLine("Acceptance criteria:");
            foreach (var item in task.AcceptanceCriteria)
            {
                sb.AppendLine($"* {item}");
            }

            sb.AppendLine();
        }

        if (task.TestCases is { Count: > 0 })
        {
            sb.AppendLine("Test cases:");
            foreach (var tc in task.TestCases)
            {
                sb.AppendLine($"* [{tc.Type}] {tc.Description} => {tc.ExpectedResult}");
            }

            sb.AppendLine();
        }

        if (task.Risks is { Count: > 0 })
        {
            sb.AppendLine("Risks:");
            foreach (var risk in task.Risks)
            {
                sb.AppendLine($"* {risk}");
            }
        }

        return sb.ToString().Trim();
    }

    private static IReadOnlyList<string> BuildLabels(ProjectTask task)
    {
        var labels = new List<string> { "skemex", task.Code.Replace(' ', '-') };
        if (task.Tags is { Count: > 0 })
        {
            labels.AddRange(task.Tags);
        }

        return labels;
    }

    private static string? FormatEstimate(int? minutes)
    {
        if (minutes is null or <= 0)
        {
            return null;
        }

        var hours = minutes.Value / 60;
        var mins = minutes.Value % 60;
        if (hours > 0 && mins > 0)
        {
            return $"{hours}h {mins}m";
        }

        if (hours > 0)
        {
            return $"{hours}h";
        }

        return $"{mins}m";
    }

    private static string FormatJiraDateTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+0000'", CultureInfo.InvariantCulture);

    private static string? BuildWorklogComment(ProjectTaskWorkLog workLog)
    {
        var who = workLog.User is null
            ? null
            : $"{workLog.User.FirstName} {workLog.User.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(who) && !string.IsNullOrWhiteSpace(workLog.User?.Email))
        {
            who = workLog.User.Email;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(who))
        {
            parts.Add($"Logged in Skemex by {who}");
        }

        if (!string.IsNullOrWhiteSpace(workLog.Comment))
        {
            parts.Add(workLog.Comment.Trim());
        }

        return parts.Count == 0 ? null : string.Join(" — ", parts);
    }

    private static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(html);
        text = BlockBreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, string.Empty);
        text = MultiNewlineRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    [GeneratedRegex(@"<(br|BR)\s*/?>|</(p|div|li|h[1-6]|tr)>", RegexOptions.Compiled)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultiNewlineRegex();
}
