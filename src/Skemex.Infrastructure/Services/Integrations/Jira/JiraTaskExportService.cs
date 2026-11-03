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
                .Include(t => t.Comments)
                .ThenInclude(c => c.Author),
            cancellationToken: cancellationToken);

        if (tasks.Count == 0)
        {
            return new ExportTasksToJiraResultDto();
        }

        // Selected export must include ancestor chain so parent links can be created in Jira.
        if (request.Scope == JiraExportScope.Selected)
        {
            tasks = await EnsureAncestorsIncludedAsync(projectId, tasks, cancellationToken);
        }

        if (tasks.Count > MaxExportTasks)
        {
            return Error.Validation(
                "Jira.ExportTooMany",
                $"Export is limited to {MaxExportTasks} tasks. Narrow filters or export a selection.");
        }

        // Parents before children so Epic → Story → Sub-task links resolve in one pass.
        tasks = OrderForTreeExport(tasks);

        var existingMappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.IntegrationConnectionId == connection.Id
                && m.LocalEntityType == "Task",
            cancellationToken: cancellationToken);

        var existingCommentMappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.IntegrationConnectionId == connection.Id
                && m.LocalEntityType == "TaskComment",
            cancellationToken: cancellationToken);
        var exportedCommentIds = existingCommentMappings
            .Select(m => m.LocalEntityId)
            .ToHashSet();

        var mappedByTaskId = existingMappings.ToDictionary(m => m.LocalEntityId);
        var localToJira = existingMappings
            .Where(m =>
                !string.IsNullOrWhiteSpace(m.ExternalEntityId)
                || !string.IsNullOrWhiteSpace(m.ExternalEntityKey))
            .ToDictionary(
                m => m.LocalEntityId,
                m => (Id: m.ExternalEntityId, Key: m.ExternalEntityKey ?? string.Empty));

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

        IReadOnlyList<JiraIssueTypeDto> issueTypes;
        try
        {
            issueTypes = await jiraApiClient.GetProjectIssueTypesAsync(
                accessToken,
                cloudId,
                projectKey,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load Jira issue types for project {ProjectKey}", projectKey);
            issueTypes = [];
        }

        var tasksById = tasks.ToDictionary(t => t.Id);
        var issueCache = new Dictionary<string, JiraIssueRefDto?>(StringComparer.OrdinalIgnoreCase);

        // Level-by-level: parents first, then children with parent.key.
        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                mappedByTaskId.TryGetValue(task.Id, out var existingMapping);
                var issueRef = existingMapping is null ? null : PreferIssueKey(existingMapping);
                var remote = string.IsNullOrWhiteSpace(issueRef)
                    ? null
                    : await GetCachedIssueAsync(
                        accessToken,
                        cloudId,
                        issueRef,
                        issueCache,
                        cancellationToken);

                if (remote is not null && existingMapping is not null)
                {
                    // Keep mapping + local index in sync with the live Jira issue.
                    existingMapping.ExternalEntityId = remote.Id;
                    existingMapping.ExternalEntityKey = remote.Key;
                    localToJira[task.Id] = (remote.Id, remote.Key);

                    var parentKey = ResolveParentKey(task, localToJira);
                    if (parentKey is not null)
                    {
                        try
                        {
                            await jiraApiClient.UpdateIssueParentAsync(
                                accessToken,
                                cloudId,
                                remote.Key,
                                parentKey,
                                cancellationToken);

                            existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
                            await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
                            var commentsLinked = await ExportCommentsAsync(
                                task,
                                remote.Key,
                                projectId,
                                connection,
                                exportedCommentIds,
                                accessToken,
                                cloudId,
                                cancellationToken);
                            exported++;
                            items.Add(new ExportTasksToJiraItemResultDto
                            {
                                TaskId = task.Id,
                                TaskCode = task.Code,
                                Status = "Updated",
                                JiraIssueKey = remote.Key,
                                Message = commentsLinked > 0
                                    ? $"Linked under parent {parentKey}; exported {commentsLinked} comment(s)."
                                    : $"Linked under parent {parentKey}.",
                            });
                            continue;
                        }
                        catch (Exception ex) when (IsJiraNotFound(ex))
                        {
                            logger.LogWarning(
                                ex,
                                "Mapped Jira issue {IssueKey} for {TaskCode} vanished; recreating",
                                remote.Key,
                                task.Code);
                            issueCache[remote.Key] = null;
                            localToJira.Remove(task.Id);
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            logger.LogWarning(
                                ex,
                                "Failed to link Jira issue {IssueKey} under parent {ParentKey}",
                                remote.Key,
                                parentKey);
                            items.Add(new ExportTasksToJiraItemResultDto
                            {
                                TaskId = task.Id,
                                TaskCode = task.Code,
                                Status = "Failed",
                                JiraIssueKey = remote.Key,
                                Message = Truncate(ex.Message, 400),
                            });
                            continue;
                        }
                    }
                    else
                    {
                        existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
                        await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
                        var commentsSynced = await ExportCommentsAsync(
                            task,
                            remote.Key,
                            projectId,
                            connection,
                            exportedCommentIds,
                            accessToken,
                            cloudId,
                            cancellationToken);
                        if (commentsSynced > 0)
                        {
                            exported++;
                            items.Add(new ExportTasksToJiraItemResultDto
                            {
                                TaskId = task.Id,
                                TaskCode = task.Code,
                                Status = "Updated",
                                JiraIssueKey = remote.Key,
                                Message = $"Exported {commentsSynced} new comment(s).",
                            });
                        }
                        else
                        {
                            skipped++;
                            items.Add(new ExportTasksToJiraItemResultDto
                            {
                                TaskId = task.Id,
                                TaskCode = task.Code,
                                Status = "Skipped",
                                JiraIssueKey = remote.Key,
                                Message = "Already exported to Jira.",
                            });
                        }

                        continue;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(issueRef))
                {
                    logger.LogWarning(
                        "Mapped Jira issue {IssueRef} for {TaskCode} is missing; recreating",
                        issueRef,
                        task.Code);
                    localToJira.Remove(task.Id);
                }

                var wasMapped = existingMapping is not null;
                var issue = await ExportOneAsync(
                    task,
                    accessToken,
                    cloudId,
                    projectKey,
                    localToJira,
                    tasksById,
                    issueCache,
                    accountCache,
                    issueTypes,
                    cancellationToken);

                if (existingMapping is not null)
                {
                    existingMapping.ExternalEntityId = issue.Id;
                    existingMapping.ExternalEntityKey = issue.Key;
                    existingMapping.LastSyncedAt = DateTimeOffset.UtcNow;
                    await mappingRepository.UpdateAsync(existingMapping, cancellationToken);
                    mappedByTaskId[task.Id] = existingMapping;
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

                localToJira[task.Id] = (issue.Id, issue.Key);
                issueCache[issue.Key] = new JiraIssueRefDto
                {
                    Id = issue.Id,
                    Key = issue.Key,
                    IssueTypeName = task.Type == ProjectTaskType.Feature ? "Epic" : "Task",
                    Subtask = false,
                };

                // Stale comment mappings pointed at a deleted Jira issue — allow re-export.
                if (wasMapped && task.Comments is { Count: > 0 })
                {
                    var commentIds = task.Comments.Select(c => c.Id).ToList();
                    foreach (var commentId in commentIds)
                    {
                        exportedCommentIds.Remove(commentId);
                    }

                    var staleCommentMappings = await mappingRepository.GetAllAsync(
                        filter: m =>
                            m.ProjectId == projectId
                            && m.IntegrationConnectionId == connection.Id
                            && m.LocalEntityType == "TaskComment"
                            && commentIds.Contains(m.LocalEntityId),
                        cancellationToken: cancellationToken);
                    foreach (var stale in staleCommentMappings)
                    {
                        await mappingRepository.DeleteAsync(stale, cancellationToken);
                    }
                }

                var commentsExported = await ExportCommentsAsync(
                    task,
                    issue.Key,
                    projectId,
                    connection,
                    exportedCommentIds,
                    accessToken,
                    cloudId,
                    cancellationToken);

                exported++;
                items.Add(new ExportTasksToJiraItemResultDto
                {
                    TaskId = task.Id,
                    TaskCode = task.Code,
                    Status = wasMapped ? "Recreated" : "Exported",
                    JiraIssueKey = issue.Key,
                    Message = wasMapped
                        ? commentsExported > 0
                            ? $"Previous Jira issue was missing; created a new one and exported {commentsExported} comment(s)."
                            : "Previous Jira issue was missing; created a new one."
                        : commentsExported > 0
                            ? $"Exported with {commentsExported} comment(s)."
                            : null,
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

    private static string? PreferIssueKey(IntegrationMapping mapping) =>
        !string.IsNullOrWhiteSpace(mapping.ExternalEntityKey)
            ? mapping.ExternalEntityKey
            : mapping.ExternalEntityId;

    private static string? ResolveParentKey(
        ProjectTask task,
        IReadOnlyDictionary<Guid, (string Id, string Key)> localToJira)
    {
        if (task.ParentId is not { } parentTaskId
            || !localToJira.TryGetValue(parentTaskId, out var mappedParent))
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(mappedParent.Key)
            ? mappedParent.Key
            : null;
    }

    private static ProjectTask? ResolveParentTask(
        ProjectTask task,
        IReadOnlyDictionary<Guid, ProjectTask> tasksById)
    {
        if (task.Parent is not null)
        {
            return task.Parent;
        }

        if (task.ParentId is { } parentId && tasksById.TryGetValue(parentId, out var parent))
        {
            return parent;
        }

        return null;
    }

    private static bool IsJiraNotFound(Exception ex) =>
        ex.Message.Contains("(404)", StringComparison.Ordinal)
        || ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private static bool IsInvalidParentError(Exception ex) =>
        ex.Message.Contains("valid parent", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("\"parent\"", StringComparison.OrdinalIgnoreCase);

    private async Task<JiraIssueRefDto?> GetCachedIssueAsync(
        string accessToken,
        string cloudId,
        string issueIdOrKey,
        Dictionary<string, JiraIssueRefDto?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(issueIdOrKey, out var cached))
        {
            return cached;
        }

        var remote = await jiraApiClient.TryGetIssueAsync(
            accessToken,
            cloudId,
            issueIdOrKey,
            cancellationToken);
        cache[issueIdOrKey] = remote;
        if (remote is not null
            && !string.Equals(issueIdOrKey, remote.Key, StringComparison.OrdinalIgnoreCase))
        {
            cache[remote.Key] = remote;
        }

        return remote;
    }

    private async Task<JiraCreatedIssueDto> ExportOneAsync(
        ProjectTask task,
        string accessToken,
        string cloudId,
        string projectKey,
        IReadOnlyDictionary<Guid, (string Id, string Key)> localToJira,
        IReadOnlyDictionary<Guid, ProjectTask> tasksById,
        Dictionary<string, JiraIssueRefDto?> issueCache,
        Dictionary<string, string?> accountCache,
        IReadOnlyList<JiraIssueTypeDto> issueTypes,
        CancellationToken cancellationToken)
    {
        var parentKey = ResolveParentKey(task, localToJira);
        var parentLocal = ResolveParentTask(task, tasksById);

        JiraIssueRefDto? parentRemote = null;
        if (parentKey is not null)
        {
            parentRemote = await GetCachedIssueAsync(
                accessToken,
                cloudId,
                parentKey,
                issueCache,
                cancellationToken);
            if (parentRemote is null)
            {
                throw new InvalidOperationException(
                    $"Parent Jira issue {parentKey} does not exist. Export/recreate the parent task first.");
            }

            parentKey = parentRemote.Key;
        }

        // Team-managed (KAN): Sub-task cannot sit under Epic — use Story/Task + parent.key.
        // Sub-task is only valid under a base-level issue (Task/Story/Bug).
        var parentIsEpic = parentLocal?.Type == ProjectTaskType.Feature
                           || parentRemote?.IsEpic == true;
        var hasJiraParent = parentKey is not null;
        var useSubtask = hasJiraParent && !parentIsEpic && parentRemote is { Subtask: false };
        var underEpic = hasJiraParent && parentIsEpic;
        var resolvedType = ResolveIssueType(task.Type, useSubtask, underEpic, issueTypes);

        logger.LogInformation(
            "Exporting {TaskCode} as Jira {IssueType} (subtask={Subtask}) under parent {ParentKey} ({ParentType})",
            task.Code,
            resolvedType.Name,
            useSubtask,
            parentKey ?? "(none)",
            parentRemote?.IssueTypeName ?? parentLocal?.Type.ToString() ?? "(none)");

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

        async Task<JiraCreatedIssueDto> CreateAsync(JiraIssueTypeDto type, string? withParentKey) =>
            await jiraApiClient.CreateIssueAsync(
                accessToken,
                cloudId,
                new JiraCreateIssueRequest
                {
                    ProjectKey = projectKey,
                    Summary = BuildSummary(task),
                    IssueTypeName = type.Name,
                    IssueTypeId = string.IsNullOrWhiteSpace(type.Id) ? null : type.Id,
                    Description = BuildDescription(task, withParentKey),
                    AssigneeAccountId = assigneeAccountId,
                    ParentIssueKey = withParentKey,
                    ParentIssueId = null,
                    Labels = BuildLabels(task),
                    OriginalEstimate = FormatEstimate(task.OriginalEstimateMinutes),
                    RemainingEstimate = FormatEstimate(task.RemainingEstimateMinutes),
                },
                cancellationToken);

        JiraCreatedIssueDto created;
        try
        {
            created = await CreateAsync(resolvedType, parentKey);
        }
        catch (Exception ex) when (parentKey is not null && IsInvalidParentError(ex))
        {
            // Wrong child type for this parent (e.g. Sub-task→Epic on team-managed).
            // Retry once as a non-subtask Story/Task under the same parent key.
            if (useSubtask || !underEpic)
            {
                var retryType = ResolveIssueType(
                    task.Type,
                    useSubtask: false,
                    underEpic: true,
                    issueTypes);
                logger.LogWarning(
                    ex,
                    "Parent {ParentKey} rejected {IssueType} for {TaskCode}; retrying as {RetryType}",
                    parentKey,
                    resolvedType.Name,
                    task.Code,
                    retryType.Name);
                created = await CreateAsync(retryType, parentKey);
            }
            else
            {
                throw;
            }
        }

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

    private async Task<List<ProjectTask>> EnsureAncestorsIncludedAsync(
        Guid projectId,
        List<ProjectTask> selected,
        CancellationToken cancellationToken)
    {
        var byId = selected.ToDictionary(t => t.Id);
        var missingParentIds = selected
            .Where(t => t.ParentId is { } pid && !byId.ContainsKey(pid))
            .Select(t => t.ParentId!.Value)
            .Distinct()
            .ToHashSet();

        while (missingParentIds.Count > 0)
        {
            var ancestors = await taskRepository.GetAllAsync(
                filter: t => t.ProjectId == projectId && missingParentIds.Contains(t.Id),
                include: query => query
                    .Include(t => t.Assignee)
                    .Include(t => t.Reporter)
                    .Include(t => t.Parent)
                    .Include(t => t.WorkLogs)
                    .ThenInclude(w => w.User)
                    .Include(t => t.Comments)
                    .ThenInclude(c => c.Author),
            cancellationToken: cancellationToken);

            missingParentIds.Clear();
            foreach (var ancestor in ancestors)
            {
                if (!byId.TryAdd(ancestor.Id, ancestor))
                {
                    continue;
                }

                if (ancestor.ParentId is { } pid && !byId.ContainsKey(pid))
                {
                    missingParentIds.Add(pid);
                }
            }
        }

        return byId.Values.ToList();
    }

    private static List<ProjectTask> OrderForTreeExport(List<ProjectTask> tasks)
    {
        var byId = tasks.ToDictionary(t => t.Id);
        var depth = new Dictionary<Guid, int>();

        int DepthOf(ProjectTask task)
        {
            if (depth.TryGetValue(task.Id, out var known))
            {
                return known;
            }

            if (task.ParentId is not { } parentId || !byId.ContainsKey(parentId))
            {
                return depth[task.Id] = 0;
            }

            // Guard cycles; treat as root.
            depth[task.Id] = -1;
            var parentDepth = DepthOf(byId[parentId]);
            return depth[task.Id] = parentDepth < 0 ? 0 : parentDepth + 1;
        }

        foreach (var task in tasks)
        {
            DepthOf(task);
        }

        return tasks
            .OrderBy(t => depth[t.Id])
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static JiraIssueTypeDto ResolveIssueType(
        ProjectTaskType type,
        bool useSubtask,
        bool underEpic,
        IReadOnlyList<JiraIssueTypeDto> available)
    {
        if (available.Count == 0)
        {
            var fallbackName = useSubtask
                ? "Sub-task"
                : type switch
                {
                    ProjectTaskType.Bug => "Bug",
                    ProjectTaskType.Feature => "Epic",
                    _ when underEpic => "Story",
                    _ => "Task",
                };
            return new JiraIssueTypeDto { Name = fallbackName, Subtask = useSubtask };
        }

        string[] preferredNames = useSubtask
            ? ["Sub-task", "Subtask", "Sub Task"]
            : type switch
            {
                ProjectTaskType.Bug => ["Bug", "Defect", "Error", "Story", "Task"],
                ProjectTaskType.Feature => ["Epic", "Feature", "Story", "Initiative"],
                // Epic children: Story is the usual hierarchy child in Jira Software.
                _ when underEpic => ["Story", "Task", "Work Item", "Issue", "Bug"],
                _ => ["Task", "Story", "Work Item", "Issue"],
            };

        foreach (var preferred in preferredNames)
        {
            var match = available.FirstOrDefault(t =>
                string.Equals(t.Name, preferred, StringComparison.OrdinalIgnoreCase)
                && (useSubtask ? t.Subtask : !t.Subtask));
            if (match is not null)
            {
                return match;
            }
        }

        if (useSubtask)
        {
            var anySubtask = available.FirstOrDefault(t => t.Subtask);
            if (anySubtask is not null)
            {
                return anySubtask;
            }
        }

        var anyStandard = available.FirstOrDefault(t => !t.Subtask);
        return anyStandard ?? available[0];
    }

    private static string BuildSummary(ProjectTask task)
    {
        var title = string.IsNullOrWhiteSpace(task.Title) ? task.Code : task.Title.Trim();
        var summary = $"[{task.Code}] {title}";
        return summary.Length <= 255 ? summary : summary[..255];
    }

    private static string BuildDescription(ProjectTask task, string? jiraParentKey = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Skemex key: {task.Code}");
        sb.AppendLine($"Type: {task.Type}");
        if (!string.IsNullOrWhiteSpace(jiraParentKey))
        {
            sb.AppendLine($"Parent Jira issue: {jiraParentKey}");
        }
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

    private async Task<int> ExportCommentsAsync(
        ProjectTask task,
        string jiraIssueKey,
        Guid projectId,
        IntegrationConnection connection,
        HashSet<Guid> exportedCommentIds,
        string accessToken,
        string cloudId,
        CancellationToken cancellationToken)
    {
        if (task.Comments is not { Count: > 0 })
        {
            return 0;
        }

        var exported = 0;
        foreach (var comment in task.Comments.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id))
        {
            if (exportedCommentIds.Contains(comment.Id))
            {
                continue;
            }

            var body = BuildIssueCommentBody(comment);
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            try
            {
                var created = await jiraApiClient.AddIssueCommentAsync(
                    accessToken,
                    cloudId,
                    jiraIssueKey,
                    new JiraAddCommentRequest { Body = body },
                    cancellationToken);

                await mappingRepository.AddAsync(
                    new IntegrationMapping
                    {
                        Id = Guid.NewGuid(),
                        TenantId = connection.TenantId,
                        ProjectId = projectId,
                        IntegrationConnectionId = connection.Id,
                        LocalEntityType = "TaskComment",
                        LocalEntityId = comment.Id,
                        ExternalEntityType = "Comment",
                        ExternalEntityId = created.Id,
                        ExternalEntityKey = $"{jiraIssueKey}:{created.Id}",
                        LastSyncedAt = DateTimeOffset.UtcNow,
                    },
                    cancellationToken);

                exportedCommentIds.Add(comment.Id);
                exported++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Comment export failed for task {TaskCode} / issue {IssueKey} / comment {CommentId}",
                    task.Code,
                    jiraIssueKey,
                    comment.Id);
            }
        }

        return exported;
    }

    private static string? BuildIssueCommentBody(ProjectTaskComment comment)
    {
        var plain = HtmlToPlainText(comment.Body);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        var who = comment.Author is null
            ? null
            : $"{comment.Author.FirstName} {comment.Author.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(who) && !string.IsNullOrWhiteSpace(comment.Author?.Email))
        {
            who = comment.Author.Email;
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(who))
        {
            sb.AppendLine($"{who}:");
        }

        sb.AppendLine(plain.Trim());
        sb.AppendLine();
        sb.Append("— Exported from Skemex");
        return sb.ToString().Trim();
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
