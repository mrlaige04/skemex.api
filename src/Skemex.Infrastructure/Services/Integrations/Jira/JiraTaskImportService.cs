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

            if (request.SkipAlreadyImported
                && (mappedByExternalId.ContainsKey(issue.Id)
                    || mappedByExternalKey.ContainsKey(issue.Key)))
            {
                skipped++;
                var existing = mappedByExternalId.GetValueOrDefault(issue.Id)
                               ?? mappedByExternalKey.GetValueOrDefault(issue.Key);
                items.Add(new ImportTasksFromJiraItemResultDto
                {
                    JiraIssueKey = issue.Key,
                    Status = "Skipped",
                    LocalTaskId = existing?.LocalEntityId,
                    Message = "Already imported.",
                });
                continue;
            }

            try
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

                jiraKeyToLocalId[issue.Key] = created.Id;
                imported++;
                items.Add(new ImportTasksFromJiraItemResultDto
                {
                    JiraIssueKey = issue.Key,
                    Status = "Imported",
                    LocalTaskCode = created.Code,
                    LocalTaskId = created.Id,
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

    private async Task<ProjectTask> ImportOneAsync(
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
        var title = string.IsNullOrWhiteSpace(issue.Summary) ? issue.Key : issue.Summary.Trim();
        if (title.Length > 500)
        {
            title = title[..500];
        }

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

        await mappingRepository.AddAsync(
            new IntegrationMapping
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
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return task;
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
