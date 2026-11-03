using System.Net;
using System.Text;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Helpers.Projects;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed class JiraTaskImportService(
    ICurrentUser currentUser,
    ITenantRepository<IntegrationConnection> connectionRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectSettings> projectSettingsRepository,
    ITenantRepository<ProjectColumn> projectColumnRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<ProjectTask> projectTaskRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTaskComment> commentRepository,
    IProjectTaskCodeAllocator taskCodeAllocator,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient,
    ILogger<JiraTaskImportService> logger) : IJiraTaskImportService
{
    public const int MaxImportIssues = 200;

    public async Task<ErrorOr<IReadOnlyList<JiraIssuePreviewDto>>> ListRemoteIssuesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadConnectedContextAsync(projectId, cancellationToken);
        if (context.IsError)
        {
            return context.Errors;
        }

        var (connection, accessToken) = context.Value;
        IReadOnlyList<JiraRemoteIssueDto> issues;
        try
        {
            issues = await jiraApiClient.SearchProjectIssuesAsync(
                accessToken,
                connection.CloudId!,
                connection.ExternalProjectKey!,
                MaxImportIssues,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list Jira issues for project {ProjectId}", projectId);
            return Error.Failure("Jira.ListFailed", Truncate(ex.Message, 400));
        }

        var mappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.IntegrationConnectionId == connection.Id
                && m.LocalEntityType == "Task",
            cancellationToken: cancellationToken);

        var byExternalId = mappings.ToDictionary(m => m.ExternalEntityId, StringComparer.Ordinal);
        var byExternalKey = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalEntityKey))
            .ToDictionary(m => m.ExternalEntityKey!, StringComparer.OrdinalIgnoreCase);

        var localIds = mappings.Select(m => m.LocalEntityId).Distinct().ToList();
        var localTasks = localIds.Count == 0
            ? []
            : await projectTaskRepository.GetAllAsync(
                filter: t => localIds.Contains(t.Id),
                cancellationToken: cancellationToken);
        var codeById = localTasks.ToDictionary(t => t.Id, t => t.Code);

        return issues
            .Select(issue =>
            {
                IntegrationMapping? mapping = null;
                if (byExternalId.TryGetValue(issue.Id, out var byId))
                {
                    mapping = byId;
                }
                else if (byExternalKey.TryGetValue(issue.Key, out var byKey))
                {
                    mapping = byKey;
                }

                string? localCode = null;
                if (mapping is not null)
                {
                    codeById.TryGetValue(mapping.LocalEntityId, out localCode);
                }

                return new JiraIssuePreviewDto
                {
                    Id = issue.Id,
                    Key = issue.Key,
                    Summary = issue.Summary,
                    IssueType = issue.IssueType,
                    Status = issue.Status,
                    AssigneeDisplayName = issue.AssigneeDisplayName,
                    ParentKey = issue.ParentKey,
                    AlreadyImported = mapping is not null,
                    LocalTaskCode = localCode,
                };
            })
            .ToList();
    }

    public async Task<ErrorOr<ImportTasksFromJiraResultDto>> ImportAsync(
        Guid projectId,
        ImportTasksFromJiraRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetTenantId();
        var reporterId = currentUser.GetUserId();
        if (tenantId is null || reporterId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace and sign in before importing.");
        }

        var context = await LoadConnectedContextAsync(projectId, cancellationToken);
        if (context.IsError)
        {
            return context.Errors;
        }

        var (connection, accessToken) = context.Value;

        var project = await projectRepository.GetAsync(
            filter: p => p.Id == projectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: s => s.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("ProjectSettings.NotFound", "Project settings were not found.");
        }

        var defaultColumn = await projectColumnRepository.GetAsync(
            filter: c => c.Id == settings.DefaultTaskColumnId && c.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (defaultColumn is null)
        {
            return Error.Validation(
                "ProjectSettings.InvalidDefaultColumn",
                "Default task column is not configured for this project.");
        }

        IReadOnlyList<JiraRemoteIssueDto> remoteIssues;
        try
        {
            remoteIssues = await jiraApiClient.SearchProjectIssuesAsync(
                accessToken,
                connection.CloudId!,
                connection.ExternalProjectKey!,
                MaxImportIssues,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Error.Failure("Jira.ListFailed", Truncate(ex.Message, 400));
        }

        if (!request.ImportAll)
        {
            var keys = (request.IssueKeys ?? [])
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (keys.Count == 0)
            {
                return Error.Validation("Jira.SelectionRequired", "Select at least one Jira issue to import.");
            }

            remoteIssues = remoteIssues.Where(i => keys.Contains(i.Key)).ToList();
        }

        if (remoteIssues.Count == 0)
        {
            return new ImportTasksFromJiraResultDto();
        }

        var mappings = await mappingRepository.GetAllAsync(
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
        var importedCommentIds = existingCommentMappings
            .Select(m => m.ExternalEntityId)
            .ToHashSet(StringComparer.Ordinal);

        var mappedByExternalKey = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalEntityKey))
            .ToDictionary(m => m.ExternalEntityKey!, StringComparer.OrdinalIgnoreCase);
        var mappedByExternalId = mappings.ToDictionary(m => m.ExternalEntityId, StringComparer.Ordinal);

        var members = await projectUserRepository.GetAllAsync(
            filter: m => m.ProjectId == projectId,
            include: q => q.Include(m => m.User),
            cancellationToken: cancellationToken);
        var userIdByEmail = members
            .Where(m => m.User is not null && !string.IsNullOrWhiteSpace(m.User.Email))
            .GroupBy(m => m.User.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.First().UserId, StringComparer.OrdinalIgnoreCase);
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

        var ordered = remoteIssues
            .OrderBy(i => string.IsNullOrWhiteSpace(i.ParentKey) ? 0 : 1)
            .ThenBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var jiraKeyToLocalId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            if (!string.IsNullOrWhiteSpace(mapping.ExternalEntityKey))
            {
                jiraKeyToLocalId[mapping.ExternalEntityKey!] = mapping.LocalEntityId;
            }
        }

        var items = new List<ImportTasksFromJiraItemResultDto>(ordered.Count);
        var imported = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var issue in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existingMapping = FindMapping(issue, mappedByExternalId, mappedByExternalKey);
            if (existingMapping is not null && request.SkipAlreadyImported)
            {
                jiraKeyToLocalId[issue.Key] = existingMapping.LocalEntityId;
                var commentsSynced = 0;
                try
                {
                    commentsSynced = await ImportCommentsAsync(
                        accessToken,
                        connection.CloudId!,
                        issue.Key,
                        existingMapping.LocalEntityId,
                        project.Id,
                        tenantId.Value,
                        connection.Id,
                        reporterId.Value,
                        userIdByEmail,
                        userIdByFullName,
                        importedCommentIds,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Comment sync failed for already-linked Jira issue {IssueKey}",
                        issue.Key);
                }

                if (commentsSynced > 0)
                {
                    imported++;
                    items.Add(new ImportTasksFromJiraItemResultDto
                    {
                        JiraIssueKey = issue.Key,
                        Status = "Updated",
                        LocalTaskId = existingMapping.LocalEntityId,
                        Message = $"Already linked; imported {commentsSynced} new comment(s).",
                    });
                }
                else
                {
                    skipped++;
                    items.Add(new ImportTasksFromJiraItemResultDto
                    {
                        JiraIssueKey = issue.Key,
                        Status = "Skipped",
                        LocalTaskId = existingMapping.LocalEntityId,
                        Message = "Already linked to a Skemex task.",
                    });
                }

                continue;
            }

            try
            {
                ProjectTask task;
                string status;
                string? message = null;

                if (existingMapping is not null)
                {
                    // Never insert a second mapping for the same Jira issue id/key.
                    task = await SyncExistingAsync(
                        issue,
                        existingMapping,
                        project,
                        tenantId.Value,
                        reporterId.Value,
                        defaultColumn.Id,
                        userIdByEmail,
                        userIdByFullName,
                        jiraKeyToLocalId,
                        cancellationToken);
                    status = "Updated";
                    message = "Existing mapping reused; local task refreshed.";
                    TrackMapping(existingMapping, mappedByExternalId, mappedByExternalKey);
                }
                else
                {
                    var created = await ImportOneAsync(
                        issue,
                        project,
                        tenantId.Value,
                        reporterId.Value,
                        defaultColumn.Id,
                        connection,
                        userIdByEmail,
                        userIdByFullName,
                        jiraKeyToLocalId,
                        cancellationToken);
                    task = created.Task;
                    TrackMapping(created.Mapping, mappedByExternalId, mappedByExternalKey);
                    status = "Imported";
                }

                var commentsImported = await ImportCommentsAsync(
                    accessToken,
                    connection.CloudId!,
                    issue.Key,
                    task.Id,
                    project.Id,
                    tenantId.Value,
                    connection.Id,
                    reporterId.Value,
                    userIdByEmail,
                    userIdByFullName,
                    importedCommentIds,
                    cancellationToken);
                if (commentsImported > 0)
                {
                    message = string.IsNullOrWhiteSpace(message)
                        ? $"Imported {commentsImported} comment(s)."
                        : $"{message} Imported {commentsImported} comment(s).";
                }

                jiraKeyToLocalId[issue.Key] = task.Id;
                imported++;
                items.Add(new ImportTasksFromJiraItemResultDto
                {
                    JiraIssueKey = issue.Key,
                    Status = status,
                    LocalTaskCode = task.Code,
                    LocalTaskId = task.Id,
                    Message = message,
                });
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Failed to import Jira issue {IssueKey}", issue.Key);
                items.Add(new ImportTasksFromJiraItemResultDto
                {
                    JiraIssueKey = issue.Key,
                    Status = "Failed",
                    Message = Truncate(ex.Message, 400),
                });
            }
        }

        return new ImportTasksFromJiraResultDto
        {
            TotalCandidates = ordered.Count,
            Imported = imported,
            Skipped = skipped,
            Failed = failed,
            Items = items,
        };
    }

    public async Task<ErrorOr<Guid>> UpsertIssueFromWebhookAsync(
        IntegrationConnection connection,
        string accessToken,
        string issueIdOrKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueIdOrKey)
            || string.IsNullOrWhiteSpace(connection.CloudId))
        {
            return Error.Validation("Jira.IssueRequired", "Jira issue key or id is required.");
        }

        var tenantId = connection.TenantId;
        var projectId = connection.ProjectId;

        var project = await projectRepository.GetAsync(
            filter: p => p.Id == projectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var settings = await projectSettingsRepository.GetAsync(
            filter: s => s.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (settings is null)
        {
            return Error.NotFound("ProjectSettings.NotFound", "Project settings were not found.");
        }

        var defaultColumn = await projectColumnRepository.GetAsync(
            filter: c => c.Id == settings.DefaultTaskColumnId && c.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (defaultColumn is null)
        {
            return Error.Validation(
                "ProjectSettings.InvalidDefaultColumn",
                "Default task column is not configured for this project.");
        }

        JiraRemoteIssueDto? remote;
        try
        {
            remote = await jiraApiClient.GetRemoteIssueAsync(
                accessToken,
                connection.CloudId!,
                issueIdOrKey.Trim(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook upsert failed to load Jira issue {Issue}", issueIdOrKey);
            return Error.Failure("Jira.IssueLoadFailed", Truncate(ex.Message, 400));
        }

        if (remote is null)
        {
            return Error.NotFound("Jira.IssueNotFound", "Jira issue was not found.");
        }

        var members = await projectUserRepository.GetAllAsync(
            filter: m => m.ProjectId == projectId,
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

        var reporterId = connection.ConnectedByUserId
            ?? members.Select(m => (Guid?)m.UserId).FirstOrDefault()
            ?? Guid.Empty;
        if (reporterId == Guid.Empty)
        {
            return Error.Validation(
                "Jira.NoProjectMember",
                "Cannot create a task from webhook without a project member to use as reporter.");
        }

        var existingMappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.IntegrationConnectionId == connection.Id
                && m.LocalEntityType == "Task"
                && (m.ExternalEntityId == remote.Id
                    || (m.ExternalEntityKey != null && m.ExternalEntityKey == remote.Key)),
            cancellationToken: cancellationToken);
        var existing = existingMappings.FirstOrDefault();

        var jiraKeyToLocalId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(remote.ParentKey))
        {
            var parentMapping = await mappingRepository.GetAsync(
                filter: m =>
                    m.IntegrationConnectionId == connection.Id
                    && m.LocalEntityType == "Task"
                    && m.ExternalEntityKey == remote.ParentKey,
                cancellationToken: cancellationToken);
            if (parentMapping is not null)
            {
                jiraKeyToLocalId[remote.ParentKey!] = parentMapping.LocalEntityId;
            }
        }

        if (existing is not null)
        {
            var task = await SyncExistingAsync(
                remote,
                existing,
                project,
                tenantId,
                reporterId,
                defaultColumn.Id,
                userIdByEmail,
                userIdByFullName,
                jiraKeyToLocalId,
                cancellationToken);
            logger.LogInformation(
                "Webhook refreshed Jira {IssueKey} → local task {TaskCode}",
                remote.Key,
                task.Code);
            return task.Id;
        }

        var created = await ImportOneAsync(
            remote,
            project,
            tenantId,
            reporterId,
            defaultColumn.Id,
            connection,
            userIdByEmail,
            userIdByFullName,
            jiraKeyToLocalId,
            cancellationToken);

        // Align column with Jira status when possible.
        var columns = await projectColumnRepository.GetAllAsync(
            filter: c => c.ProjectId == projectId,
            cancellationToken: cancellationToken);
        var statusColumn = ResolveWebhookColumnId(remote.Status, columns);
        if (statusColumn is not null && statusColumn != created.Task.ProjectColumnId)
        {
            created.Task.ProjectColumnId = statusColumn.Value;
            await projectTaskRepository.UpdateAsync(created.Task, cancellationToken);
        }

        logger.LogInformation(
            "Webhook created local task {TaskCode} from Jira {IssueKey}",
            created.Task.Code,
            remote.Key);
        return created.Task.Id;
    }

    private static Guid? ResolveWebhookColumnId(string? jiraStatus, IReadOnlyList<ProjectColumn> columns)
    {
        if (string.IsNullOrWhiteSpace(jiraStatus) || columns.Count == 0)
        {
            return null;
        }

        static string Normalize(string value) =>
            string.Join(
                ' ',
                value.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ')
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var normalized = Normalize(jiraStatus);
        foreach (var column in columns)
        {
            if (Normalize(column.Title) == normalized
                || Normalize(column.Key) == normalized
                || Normalize(column.Key.Replace('-', ' ')) == normalized)
            {
                return column.Id;
            }
        }

        var aliasKey = normalized switch
        {
            "backlog" => "backlog",
            "to do" or "todo" or "open" or "new" => "todo",
            "in progress" or "inprogress" or "progress" or "doing" => "in-progress",
            "in qa" or "inqa" or "qa" or "testing" or "in review" or "review" => "in-qa",
            "done" or "closed" or "resolved" or "complete" or "completed" => "done",
            _ => null,
        };

        return aliasKey is null
            ? null
            : columns.FirstOrDefault(c => string.Equals(c.Key, aliasKey, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static IntegrationMapping? FindMapping(
        JiraRemoteIssueDto issue,
        IReadOnlyDictionary<string, IntegrationMapping> byExternalId,
        IReadOnlyDictionary<string, IntegrationMapping> byExternalKey)
    {
        if (byExternalId.TryGetValue(issue.Id, out var byId))
        {
            return byId;
        }

        if (byExternalKey.TryGetValue(issue.Key, out var byKey))
        {
            return byKey;
        }

        return null;
    }

    private static void TrackMapping(
        IntegrationMapping mapping,
        Dictionary<string, IntegrationMapping> byExternalId,
        Dictionary<string, IntegrationMapping> byExternalKey)
    {
        if (!string.IsNullOrWhiteSpace(mapping.ExternalEntityId))
        {
            byExternalId[mapping.ExternalEntityId] = mapping;
        }

        if (!string.IsNullOrWhiteSpace(mapping.ExternalEntityKey))
        {
            byExternalKey[mapping.ExternalEntityKey!] = mapping;
        }
    }

    private async Task<ProjectTask> SyncExistingAsync(
        JiraRemoteIssueDto issue,
        IntegrationMapping mapping,
        Project project,
        Guid tenantId,
        Guid reporterId,
        Guid defaultColumnId,
        IReadOnlyDictionary<string, Guid> userIdByEmail,
        IReadOnlyDictionary<string, Guid> userIdByFullName,
        IReadOnlyDictionary<string, Guid> jiraKeyToLocalId,
        CancellationToken cancellationToken)
    {
        Guid? parentId = null;
        var columnId = defaultColumnId;
        if (!string.IsNullOrWhiteSpace(issue.ParentKey)
            && jiraKeyToLocalId.TryGetValue(issue.ParentKey, out var mappedParentId))
        {
            parentId = mappedParentId;
            var parent = await projectTaskRepository.GetAsync(
                filter: t => t.Id == mappedParentId,
                cancellationToken: cancellationToken);
            if (parent is not null)
            {
                columnId = parent.ProjectColumnId;
            }
        }

        var assigneeId = ResolveLocalUserId(
            issue.AssigneeEmail,
            issue.AssigneeDisplayName,
            userIdByEmail,
            userIdByFullName);
        var type = MapIssueType(issue.IssueType, parentId is not null);
        var title = NormalizeTitle(issue);

        var task = await projectTaskRepository.GetAsync(
            filter: t => t.Id == mapping.LocalEntityId,
            cancellationToken: cancellationToken);

        if (task is null)
        {
            // Mapping survived but local task was deleted — recreate task, keep mapping row.
            await using var recreateTx = await projectTaskRepository.BeginTransactionAsync(cancellationToken);
            var taskNumber = await taskCodeAllocator.AllocateNextNumberAsync(
                tenantId,
                project.Id,
                cancellationToken);

            task = new ProjectTask
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = project.Id,
                ProjectColumnId = columnId,
                ParentId = parentId,
                Code = ProjectTaskCodeHelper.Format(project.Code, taskNumber),
                Title = title,
                Type = type,
                Description = string.IsNullOrWhiteSpace(issue.Description) ? null : issue.Description.Trim(),
                Tags = issue.Labels.Take(50).ToList(),
                OriginalEstimateMinutes = issue.OriginalEstimateMinutes,
                RemainingEstimateMinutes = issue.RemainingEstimateMinutes,
                SpentMinutes = issue.TimeSpentMinutes ?? issue.Worklogs.Sum(w => w.SpentMinutes),
                AssigneeId = assigneeId,
                ReporterId = reporterId,
            };
            await projectTaskRepository.AddAsync(task, cancellationToken);

            mapping.LocalEntityId = task.Id;
            mapping.ExternalEntityId = issue.Id;
            mapping.ExternalEntityKey = issue.Key;
            mapping.LastSyncedAt = DateTimeOffset.UtcNow;
            await mappingRepository.UpdateAsync(mapping, cancellationToken);
            await recreateTx.CommitAsync(cancellationToken);
            return task;
        }

        task.Title = title;
        task.Type = type;
        task.Description = string.IsNullOrWhiteSpace(issue.Description) ? null : issue.Description.Trim();
        task.Tags = issue.Labels.Take(50).ToList();
        task.OriginalEstimateMinutes = issue.OriginalEstimateMinutes;
        task.RemainingEstimateMinutes = issue.RemainingEstimateMinutes;
        task.AssigneeId = assigneeId;
        if (parentId is not null)
        {
            task.ParentId = parentId;
            task.ProjectColumnId = columnId;
        }

        await projectTaskRepository.UpdateAsync(task, cancellationToken);

        mapping.ExternalEntityId = issue.Id;
        mapping.ExternalEntityKey = issue.Key;
        mapping.LastSyncedAt = DateTimeOffset.UtcNow;
        await mappingRepository.UpdateAsync(mapping, cancellationToken);

        return task;
    }

    private async Task<(ProjectTask Task, IntegrationMapping Mapping)> ImportOneAsync(
        JiraRemoteIssueDto issue,
        Project project,
        Guid tenantId,
        Guid reporterId,
        Guid defaultColumnId,
        IntegrationConnection connection,
        IReadOnlyDictionary<string, Guid> userIdByEmail,
        IReadOnlyDictionary<string, Guid> userIdByFullName,
        IReadOnlyDictionary<string, Guid> jiraKeyToLocalId,
        CancellationToken cancellationToken)
    {
        Guid? parentId = null;
        var columnId = defaultColumnId;
        if (!string.IsNullOrWhiteSpace(issue.ParentKey)
            && jiraKeyToLocalId.TryGetValue(issue.ParentKey, out var mappedParentId))
        {
            parentId = mappedParentId;
            var parent = await projectTaskRepository.GetAsync(
                filter: t => t.Id == mappedParentId,
                cancellationToken: cancellationToken);
            if (parent is not null)
            {
                columnId = parent.ProjectColumnId;
            }
        }

        Guid? assigneeId = ResolveLocalUserId(
            issue.AssigneeEmail,
            issue.AssigneeDisplayName,
            userIdByEmail,
            userIdByFullName);

        var type = MapIssueType(issue.IssueType, parentId is not null);
        var title = NormalizeTitle(issue);

        await using var transaction = await projectTaskRepository.BeginTransactionAsync(cancellationToken);
        var taskNumber = await taskCodeAllocator.AllocateNextNumberAsync(
            tenantId,
            project.Id,
            cancellationToken);

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            ProjectColumnId = columnId,
            ParentId = parentId,
            Code = ProjectTaskCodeHelper.Format(project.Code, taskNumber),
            Title = title,
            Type = type,
            Description = string.IsNullOrWhiteSpace(issue.Description) ? null : issue.Description.Trim(),
            Tags = issue.Labels.Take(50).ToList(),
            OriginalEstimateMinutes = issue.OriginalEstimateMinutes,
            RemainingEstimateMinutes = issue.RemainingEstimateMinutes,
            SpentMinutes = issue.TimeSpentMinutes ?? issue.Worklogs.Sum(w => w.SpentMinutes),
            AssigneeId = assigneeId,
            ReporterId = reporterId,
        };

        await projectTaskRepository.AddAsync(task, cancellationToken);

        foreach (var wl in issue.Worklogs)
        {
            var ended = wl.StartedAt.AddMinutes(Math.Max(1, wl.SpentMinutes));
            var workLogUserId = reporterId;
            if (!string.IsNullOrWhiteSpace(wl.AuthorEmail)
                && userIdByEmail.TryGetValue(wl.AuthorEmail, out var authorId))
            {
                workLogUserId = authorId;
            }

            await workLogRepository.AddAsync(
                new ProjectTaskWorkLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = project.Id,
                    TaskId = task.Id,
                    UserId = workLogUserId,
                    StartedAt = DateTime.SpecifyKind(wl.StartedAt, DateTimeKind.Utc),
                    EndedAt = DateTime.SpecifyKind(ended, DateTimeKind.Utc),
                    SpentMinutes = Math.Max(1, wl.SpentMinutes),
                    Comment = string.IsNullOrWhiteSpace(wl.Comment)
                        ? $"Imported from Jira {issue.Key}"
                        : wl.Comment.Trim(),
                },
                cancellationToken);
        }

        var mapping = new IntegrationMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            IntegrationConnectionId = connection.Id,
            LocalEntityType = "Task",
            LocalEntityId = task.Id,
            ExternalEntityType = "Issue",
            ExternalEntityId = issue.Id,
            ExternalEntityKey = issue.Key,
            LastSyncedAt = DateTimeOffset.UtcNow,
        };
        await mappingRepository.AddAsync(mapping, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (task, mapping);
    }

    private static string NormalizeTitle(JiraRemoteIssueDto issue)
    {
        var title = string.IsNullOrWhiteSpace(issue.Summary) ? issue.Key : issue.Summary.Trim();
        return title.Length > 500 ? title[..500] : title;
    }

    private async Task<ErrorOr<(IntegrationConnection Connection, string AccessToken)>> LoadConnectedContextAsync(
        Guid projectId,
        CancellationToken cancellationToken)
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
                "Connect Jira and map a project before importing tasks.");
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        return (connection, tokenResult.Value);
    }

    private static Guid? ResolveLocalUserId(
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

    private async Task<int> ImportCommentsAsync(
        string accessToken,
        string cloudId,
        string issueKey,
        Guid taskId,
        Guid projectId,
        Guid tenantId,
        Guid connectionId,
        Guid fallbackAuthorId,
        IReadOnlyDictionary<string, Guid> userIdByEmail,
        IReadOnlyDictionary<string, Guid> userIdByFullName,
        HashSet<string> importedCommentIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JiraRemoteCommentDto> remoteComments;
        try
        {
            remoteComments = await jiraApiClient.GetIssueCommentsAsync(
                accessToken,
                cloudId,
                issueKey,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load Jira comments for issue {IssueKey}", issueKey);
            return 0;
        }

        if (remoteComments.Count == 0)
        {
            return 0;
        }

        var imported = 0;
        foreach (var remote in remoteComments
                     .OrderBy(c => c.CreatedAt ?? DateTime.MaxValue)
                     .ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (importedCommentIds.Contains(remote.Id))
            {
                continue;
            }

            var html = PlainTextToRichHtml(remote.Body);
            if (string.IsNullOrWhiteSpace(html))
            {
                continue;
            }

            if (html.Length > ProjectTaskCommentDtoHelper.MaxBodyLength)
            {
                html = html[..ProjectTaskCommentDtoHelper.MaxBodyLength];
            }

            var authorId = ResolveLocalUserId(
                               remote.AuthorEmail,
                               remote.AuthorDisplayName,
                               userIdByEmail,
                               userIdByFullName)
                           ?? fallbackAuthorId;

            var comment = new ProjectTaskComment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProjectId = projectId,
                TaskId = taskId,
                AuthorId = authorId,
                Body = html,
            };
            await commentRepository.AddAsync(comment, cancellationToken);

            await mappingRepository.AddAsync(
                new IntegrationMapping
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProjectId = projectId,
                    IntegrationConnectionId = connectionId,
                    LocalEntityType = "TaskComment",
                    LocalEntityId = comment.Id,
                    ExternalEntityType = "Comment",
                    ExternalEntityId = remote.Id,
                    ExternalEntityKey = $"{issueKey}:{remote.Id}",
                    LastSyncedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);

            importedCommentIds.Add(remote.Id);
            imported++;
        }

        return imported;
    }

    /// <summary>Wraps Jira plain-text comments as simple Quill-compatible HTML.</summary>
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

        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
