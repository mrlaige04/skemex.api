using System.Net;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Integrations.Jira;

public sealed class JiraEntityMappingService(
    ITenantRepository<ProjectIntegration> integrationRepository,
    ITenantRepository<IntegrationMapping> mappingRepository,
    ITenantRepository<ProjectTask> taskRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    ITenantRepository<ProjectTaskComment> commentRepository,
    ITenantRepository<ProjectTaskWorkLog> workLogRepository,
    ITenantRepository<ProjectTaskAttachment> attachmentRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient,
    IJiraTaskImportService importService,
    ILogger<JiraEntityMappingService> logger) : IJiraEntityMappingService
{
    private const int MaxCommentParentIssues = 40;

    public async Task<ErrorOr<JiraEntityMappingWorkspaceDto>> GetWorkspaceAsync(
        Guid projectId,
        string localEntityType,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeLocalType(localEntityType);
        if (normalized is null)
        {
            return Error.Validation("Jira.Mapping.InvalidType", "Unsupported local entity type.");
        }

        var connection = await RequireConnectedAsync(projectId, cancellationToken);
        if (connection.IsError)
        {
            return connection.Errors;
        }

        var integration = connection.Value;
        var externalType = ToExternalType(normalized);

        var mappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.ProjectIntegrationId == integration.Id
                && m.LocalEntityType == normalized,
            cancellationToken: cancellationToken);

        var mappingByLocalId = mappings.ToDictionary(m => m.LocalEntityId);

        var localItems = normalized switch
        {
            IntegrationLocalEntityTypes.Task =>
                await BuildTaskLocalsAsync(projectId, mappingByLocalId, cancellationToken),
            IntegrationLocalEntityTypes.ProjectUser =>
                await BuildProjectUserLocalsAsync(projectId, mappingByLocalId, cancellationToken),
            IntegrationLocalEntityTypes.TaskComment =>
                await BuildCommentLocalsAsync(projectId, mappingByLocalId, cancellationToken),
            IntegrationLocalEntityTypes.TaskWorkLog =>
                await BuildWorkLogLocalsAsync(projectId, mappingByLocalId, cancellationToken),
            IntegrationLocalEntityTypes.TaskAttachment =>
                await BuildAttachmentLocalsAsync(projectId, mappingByLocalId, cancellationToken),
            _ => (IReadOnlyList<JiraMappingLocalItemDto>)[],
        };

        var remoteResult = await BuildRemotesAsync(
            projectId,
            integration,
            normalized,
            mappings,
            cancellationToken);
        if (remoteResult.IsError)
        {
            return remoteResult.Errors;
        }

        return new JiraEntityMappingWorkspaceDto
        {
            LocalEntityType = normalized,
            ExternalEntityType = externalType,
            LocalItems = localItems,
            RemoteItems = remoteResult.Value,
        };
    }

    public async Task<ErrorOr<JiraMappingLinkDto>> UpsertAsync(
        Guid projectId,
        UpsertJiraEntityMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeLocalType(request.LocalEntityType);
        if (normalized is null)
        {
            return Error.Validation("Jira.Mapping.InvalidType", "Unsupported local entity type.");
        }

        if (request.LocalEntityId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.ExternalEntityId))
        {
            return Error.Validation(
                "Jira.Mapping.Required",
                "Local entity and Jira entity id are required.");
        }

        var connection = await RequireConnectedAsync(projectId, cancellationToken);
        if (connection.IsError)
        {
            return connection.Errors;
        }

        var integration = connection.Value;
        var externalType = ToExternalType(normalized);
        var status = ParseStatus(request.Status);

        var localOk = await EnsureLocalExistsAsync(
            projectId,
            normalized,
            request.LocalEntityId,
            cancellationToken);
        if (localOk.IsError)
        {
            return localOk.Errors;
        }

        var existingForLocal = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.Provider == IntegrationProvider.Jira
                && m.LocalEntityType == normalized
                && m.LocalEntityId == request.LocalEntityId,
            cancellationToken: cancellationToken);

        var conflictExternal = await mappingRepository.GetAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.ProjectIntegrationId == integration.Id
                && m.ExternalEntityType == externalType
                && m.ExternalEntityId == request.ExternalEntityId.Trim()
                && (existingForLocal == null || m.Id != existingForLocal.Id),
            cancellationToken: cancellationToken);

        if (conflictExternal is not null)
        {
            return Error.Conflict(
                "Jira.Mapping.ExternalTaken",
                "That Jira entity is already mapped to another local entity.");
        }

        IntegrationMapping mapping;
        if (existingForLocal is null)
        {
            mapping = CreateMapping(
                integration,
                normalized,
                request.LocalEntityId,
                externalType,
                request.ExternalEntityId.Trim(),
                request.ExternalEntityKey?.Trim(),
                status);
            await mappingRepository.AddAsync(mapping, cancellationToken);
        }
        else
        {
            mapping = existingForLocal;
            mapping.ExternalEntityId = request.ExternalEntityId.Trim();
            mapping.ExternalEntityKey = string.IsNullOrWhiteSpace(request.ExternalEntityKey)
                ? mapping.ExternalEntityKey
                : request.ExternalEntityKey.Trim();
            mapping.Status = status;
            mapping.LastSyncedAt = DateTimeOffset.UtcNow;
            BindLocalFk(mapping, normalized, request.LocalEntityId);
            await mappingRepository.UpdateAsync(mapping, cancellationToken);
        }

        return ToLinkDto(mapping);
    }

    public async Task<ErrorOr<Success>> DeleteAsync(
        Guid projectId,
        Guid mappingId,
        CancellationToken cancellationToken = default)
    {
        var mapping = await mappingRepository.GetAsync(
            filter: m => m.Id == mappingId && m.ProjectId == projectId,
            cancellationToken: cancellationToken);
        if (mapping is null)
        {
            return Error.NotFound("Jira.Mapping.NotFound", "Mapping was not found.");
        }

        await mappingRepository.DeleteAsync(mapping, cancellationToken);
        return Result.Success;
    }

    private async Task<ErrorOr<ProjectIntegration>> RequireConnectedAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var integration = await integrationRepository.GetAsync(
            filter: c => c.ProjectId == projectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (integration is null
            || integration.Status != IntegrationStatus.Connected
            || string.IsNullOrWhiteSpace(integration.CloudId)
            || string.IsNullOrWhiteSpace(integration.ExternalProjectKey))
        {
            return Error.Validation(
                "Jira.NotConnected",
                "Connect Jira and map a project before managing entity mappings.");
        }

        return integration;
    }

    private async Task<ErrorOr<IReadOnlyList<JiraMappingRemoteItemDto>>> BuildRemotesAsync(
        Guid projectId,
        ProjectIntegration integration,
        string localType,
        IReadOnlyList<IntegrationMapping> mappings,
        CancellationToken cancellationToken)
    {
        if (localType == IntegrationLocalEntityTypes.Task)
        {
            return await BuildIssueRemotesAsync(projectId, cancellationToken);
        }

        if (localType == IntegrationLocalEntityTypes.ProjectUser)
        {
            return await BuildUserRemotesAsync(projectId, cancellationToken);
        }

        if (localType == IntegrationLocalEntityTypes.TaskComment)
        {
            return await BuildCommentRemotesAsync(projectId, integration, mappings, cancellationToken);
        }

        if (localType is IntegrationLocalEntityTypes.TaskWorkLog or IntegrationLocalEntityTypes.TaskAttachment)
        {
            return BuildRemotesFromMappings(mappings).ToList();
        }

        return new List<JiraMappingRemoteItemDto>();
    }

    private async Task<ErrorOr<IReadOnlyList<JiraMappingRemoteItemDto>>> BuildIssueRemotesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var issues = await importService.ListRemoteIssuesAsync(projectId, cancellationToken);
        if (issues.IsError)
        {
            return issues.Errors;
        }

        var mappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.LocalEntityType == IntegrationLocalEntityTypes.Task,
            cancellationToken: cancellationToken);
        var byExternalId = mappings.ToDictionary(m => m.ExternalEntityId, StringComparer.Ordinal);
        var byExternalKey = mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.ExternalEntityKey))
            .ToDictionary(m => m.ExternalEntityKey!, StringComparer.OrdinalIgnoreCase);

        return issues.Value
            .Select(i =>
            {
                Guid? mappedLocal = null;
                if (byExternalId.TryGetValue(i.Id, out var byId))
                {
                    mappedLocal = byId.LocalEntityId;
                }
                else if (!string.IsNullOrWhiteSpace(i.Key)
                         && byExternalKey.TryGetValue(i.Key, out var byKey))
                {
                    mappedLocal = byKey.LocalEntityId;
                }

                return new JiraMappingRemoteItemDto
                {
                    Id = i.Id,
                    Key = i.Key,
                    DisplayName = string.IsNullOrWhiteSpace(i.Summary) ? i.Key : $"{i.Key} — {i.Summary}",
                    SecondaryText = i.Status,
                    MappedLocalEntityId = mappedLocal,
                };
            })
            .ToList();
    }

    private async Task<ErrorOr<IReadOnlyList<JiraMappingRemoteItemDto>>> BuildUserRemotesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var connection = await RequireConnectedAsync(projectId, cancellationToken);
        if (connection.IsError)
        {
            return connection.Errors;
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection.Value, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        IReadOnlyList<JiraUserDto> users;
        try
        {
            users = await jiraApiClient.GetAssignableUsersAsync(
                tokenResult.Value,
                connection.Value.CloudId!,
                connection.Value.ExternalProjectKey!,
                maxResults: 200,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to list Jira users for mapping workspace");
            return Error.Failure("Jira.ListUsersFailed", Truncate(ex.Message, 400));
        }

        var mappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.LocalEntityType == IntegrationLocalEntityTypes.ProjectUser,
            cancellationToken: cancellationToken);
        var byExternal = mappings.ToDictionary(m => m.ExternalEntityId, StringComparer.Ordinal);

        return users
            .Select(u => new JiraMappingRemoteItemDto
            {
                Id = u.AccountId,
                Key = u.AccountId,
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.AccountId : u.DisplayName!,
                SecondaryText = u.EmailAddress,
                MappedLocalEntityId = byExternal.TryGetValue(u.AccountId, out var m) ? m.LocalEntityId : null,
            })
            .ToList();
    }

    private async Task<ErrorOr<IReadOnlyList<JiraMappingRemoteItemDto>>> BuildCommentRemotesAsync(
        Guid projectId,
        ProjectIntegration integration,
        IReadOnlyList<IntegrationMapping> commentMappings,
        CancellationToken cancellationToken)
    {
        var tokenResult = await tokenService.GetValidAccessTokenAsync(integration, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        var taskMappings = await mappingRepository.GetAllAsync(
            filter: m =>
                m.ProjectId == projectId
                && m.ProjectIntegrationId == integration.Id
                && m.LocalEntityType == IntegrationLocalEntityTypes.Task,
            cancellationToken: cancellationToken);

        var issueKeys = taskMappings
            .Select(m => m.ExternalEntityKey ?? m.ExternalEntityId)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCommentParentIssues)
            .ToList();

        var mappedByExternal = commentMappings.ToDictionary(
            m => m.ExternalEntityId,
            StringComparer.Ordinal);

        var remotes = new Dictionary<string, JiraMappingRemoteItemDto>(StringComparer.Ordinal);
        foreach (var issueKey in issueKeys)
        {
            try
            {
                var comments = await jiraApiClient.GetIssueCommentsAsync(
                    tokenResult.Value,
                    integration.CloudId!,
                    issueKey!,
                    cancellationToken);
                foreach (var comment in comments)
                {
                    if (string.IsNullOrWhiteSpace(comment.Id))
                    {
                        continue;
                    }

                    var preview = Truncate(comment.Body, 80);
                    remotes[comment.Id] = new JiraMappingRemoteItemDto
                    {
                        Id = comment.Id,
                        Key = comment.Id,
                        DisplayName = string.IsNullOrWhiteSpace(preview)
                            ? $"Comment {comment.Id}"
                            : preview,
                        SecondaryText = $"{issueKey} · {comment.AuthorDisplayName ?? comment.AuthorEmail}",
                        MappedLocalEntityId = mappedByExternal.TryGetValue(comment.Id, out var m)
                            ? m.LocalEntityId
                            : null,
                    };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to list Jira comments for issue {IssueKey}", issueKey);
            }
        }

        // Ensure currently mapped remotes appear even if parent issue fetch failed.
        foreach (var mapping in commentMappings)
        {
            if (remotes.ContainsKey(mapping.ExternalEntityId))
            {
                continue;
            }

            remotes[mapping.ExternalEntityId] = new JiraMappingRemoteItemDto
            {
                Id = mapping.ExternalEntityId,
                Key = mapping.ExternalEntityKey ?? mapping.ExternalEntityId,
                DisplayName = mapping.ExternalEntityKey ?? mapping.ExternalEntityId,
                MappedLocalEntityId = mapping.LocalEntityId,
            };
        }

        return remotes.Values.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<JiraMappingRemoteItemDto> BuildRemotesFromMappings(
        IReadOnlyList<IntegrationMapping> mappings) =>
        mappings
            .GroupBy(m => m.ExternalEntityId, StringComparer.Ordinal)
            .Select(g =>
            {
                var m = g.First();
                return new JiraMappingRemoteItemDto
                {
                    Id = m.ExternalEntityId,
                    Key = m.ExternalEntityKey ?? m.ExternalEntityId,
                    DisplayName = m.ExternalEntityKey ?? m.ExternalEntityId,
                    MappedLocalEntityId = m.LocalEntityId,
                };
            })
            .OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<IReadOnlyList<JiraMappingLocalItemDto>> BuildTaskLocalsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, IntegrationMapping> mappingByLocalId,
        CancellationToken cancellationToken)
    {
        var tasks = await taskRepository.GetAllAsync(
            filter: t => t.ProjectId == projectId,
            cancellationToken: cancellationToken);

        return tasks
            .OrderBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .Select(t => new JiraMappingLocalItemDto
            {
                Id = t.Id,
                DisplayName = $"{t.Code} — {t.Title}",
                SecondaryText = t.Type.ToString(),
                Mapping = mappingByLocalId.TryGetValue(t.Id, out var m) ? ToLinkDto(m) : null,
            }).ToList();
    }

    private async Task<IReadOnlyList<JiraMappingLocalItemDto>> BuildProjectUserLocalsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, IntegrationMapping> mappingByLocalId,
        CancellationToken cancellationToken)
    {
        var members = await projectUserRepository.GetAllAsync(
            filter: pu => pu.ProjectId == projectId,
            include: q => q.Include(pu => pu.User),
            cancellationToken: cancellationToken);

        return members
            .OrderBy(pu => pu.User.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pu => pu.User.FirstName, StringComparer.OrdinalIgnoreCase)
            .Select(pu =>
            {
                var name = FormatUserName(pu.User);
                return new JiraMappingLocalItemDto
                {
                    Id = pu.Id,
                    DisplayName = name,
                    SecondaryText = pu.User.Email,
                    Mapping = mappingByLocalId.TryGetValue(pu.Id, out var m) ? ToLinkDto(m) : null,
                };
            }).ToList();
    }

    private async Task<IReadOnlyList<JiraMappingLocalItemDto>> BuildCommentLocalsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, IntegrationMapping> mappingByLocalId,
        CancellationToken cancellationToken)
    {
        var comments = await commentRepository.GetAllAsync(
            filter: c => c.ProjectId == projectId,
            include: q => q.Include(c => c.Task).Include(c => c.Author),
            cancellationToken: cancellationToken);

        return comments
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new JiraMappingLocalItemDto
            {
                Id = c.Id,
                DisplayName = Truncate(StripHtml(c.Body), 100),
                SecondaryText = FormatUserName(c.Author),
                ParentTaskId = c.TaskId,
                ParentTaskCode = c.Task.Code,
                Mapping = mappingByLocalId.TryGetValue(c.Id, out var m) ? ToLinkDto(m) : null,
            }).ToList();
    }

    private async Task<IReadOnlyList<JiraMappingLocalItemDto>> BuildWorkLogLocalsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, IntegrationMapping> mappingByLocalId,
        CancellationToken cancellationToken)
    {
        var logs = await workLogRepository.GetAllAsync(
            filter: w => w.ProjectId == projectId,
            include: q => q.Include(w => w.Task).Include(w => w.User),
            cancellationToken: cancellationToken);

        return logs
            .OrderByDescending(w => w.StartedAt)
            .Select(w => new JiraMappingLocalItemDto
            {
                Id = w.Id,
                DisplayName = $"{w.SpentMinutes}m · {FormatUserName(w.User)}",
                SecondaryText = Truncate(w.Comment, 80),
                ParentTaskId = w.TaskId,
                ParentTaskCode = w.Task.Code,
                Mapping = mappingByLocalId.TryGetValue(w.Id, out var m) ? ToLinkDto(m) : null,
            }).ToList();
    }

    private async Task<IReadOnlyList<JiraMappingLocalItemDto>> BuildAttachmentLocalsAsync(
        Guid projectId,
        IReadOnlyDictionary<Guid, IntegrationMapping> mappingByLocalId,
        CancellationToken cancellationToken)
    {
        var attachments = await attachmentRepository.GetAllAsync(
            filter: a => a.ProjectId == projectId,
            include: q => q.Include(a => a.Task),
            cancellationToken: cancellationToken);

        return attachments
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new JiraMappingLocalItemDto
            {
                Id = a.Id,
                DisplayName = a.FileName,
                SecondaryText = $"{a.ContentType} · {FormatBytes(a.FileSizeBytes)}",
                ParentTaskId = a.TaskId,
                ParentTaskCode = a.Task.Code,
                Mapping = mappingByLocalId.TryGetValue(a.Id, out var m) ? ToLinkDto(m) : null,
            }).ToList();
    }

    private async Task<ErrorOr<Success>> EnsureLocalExistsAsync(
        Guid projectId,
        string localType,
        Guid localId,
        CancellationToken cancellationToken)
    {
        bool exists = localType switch
        {
            IntegrationLocalEntityTypes.Task =>
                await taskRepository.GetAsync(t => t.Id == localId && t.ProjectId == projectId, cancellationToken: cancellationToken) is not null,
            IntegrationLocalEntityTypes.ProjectUser =>
                await projectUserRepository.GetAsync(pu => pu.Id == localId && pu.ProjectId == projectId, cancellationToken: cancellationToken) is not null,
            IntegrationLocalEntityTypes.TaskComment =>
                await commentRepository.GetAsync(c => c.Id == localId && c.ProjectId == projectId, cancellationToken: cancellationToken) is not null,
            IntegrationLocalEntityTypes.TaskWorkLog =>
                await workLogRepository.GetAsync(w => w.Id == localId && w.ProjectId == projectId, cancellationToken: cancellationToken) is not null,
            IntegrationLocalEntityTypes.TaskAttachment =>
                await attachmentRepository.GetAsync(a => a.Id == localId && a.ProjectId == projectId, cancellationToken: cancellationToken) is not null,
            _ => false,
        };

        return exists
            ? Result.Success
            : Error.NotFound("Jira.Mapping.LocalNotFound", "Local entity was not found in this project.");
    }

    private static IntegrationMapping CreateMapping(
        ProjectIntegration integration,
        string localType,
        Guid localId,
        string externalType,
        string externalId,
        string? externalKey,
        IntegrationMappingStatus status)
    {
        var mapping = localType switch
        {
            IntegrationLocalEntityTypes.Task =>
                IntegrationMappingFactory.ForTask(
                    integration.TenantId, integration.ProjectId, integration.Id, localId, externalId, externalKey),
            IntegrationLocalEntityTypes.ProjectUser =>
                IntegrationMappingFactory.ForProjectUser(
                    integration.TenantId, integration.ProjectId, integration.Id, localId, externalId, externalKey),
            IntegrationLocalEntityTypes.TaskComment =>
                IntegrationMappingFactory.ForComment(
                    integration.TenantId, integration.ProjectId, integration.Id, localId, externalId, externalKey),
            IntegrationLocalEntityTypes.TaskWorkLog =>
                IntegrationMappingFactory.ForWorkLog(
                    integration.TenantId, integration.ProjectId, integration.Id, localId, externalId, externalKey),
            IntegrationLocalEntityTypes.TaskAttachment =>
                IntegrationMappingFactory.ForAttachment(
                    integration.TenantId, integration.ProjectId, integration.Id, localId, externalId, externalKey),
            _ => throw new InvalidOperationException($"Unsupported type {localType}"),
        };
        mapping.Status = status;
        return mapping;
    }

    private static void BindLocalFk(IntegrationMapping mapping, string localType, Guid localId)
    {
        mapping.LocalEntityType = localType;
        mapping.LocalEntityId = localId;
        mapping.TaskId = localType == IntegrationLocalEntityTypes.Task ? localId : null;
        mapping.ProjectUserId = localType == IntegrationLocalEntityTypes.ProjectUser ? localId : null;
        mapping.TaskCommentId = localType == IntegrationLocalEntityTypes.TaskComment ? localId : null;
        mapping.TaskWorkLogId = localType == IntegrationLocalEntityTypes.TaskWorkLog ? localId : null;
        mapping.TaskAttachmentId = localType == IntegrationLocalEntityTypes.TaskAttachment ? localId : null;
    }

    private static JiraMappingLinkDto ToLinkDto(IntegrationMapping mapping) =>
        new()
        {
            Id = mapping.Id,
            Status = mapping.Status.ToString(),
            ExternalEntityId = mapping.ExternalEntityId,
            ExternalEntityKey = mapping.ExternalEntityKey,
            LastSyncedAt = mapping.LastSyncedAt,
        };

    private static string? NormalizeLocalType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            IntegrationLocalEntityTypes.Task or "ProjectTask" or "task" or "tasks" =>
                IntegrationLocalEntityTypes.Task,
            IntegrationLocalEntityTypes.ProjectUser or "user" or "users" =>
                IntegrationLocalEntityTypes.ProjectUser,
            IntegrationLocalEntityTypes.TaskComment or "comment" or "comments" =>
                IntegrationLocalEntityTypes.TaskComment,
            IntegrationLocalEntityTypes.TaskWorkLog or "worklog" or "worklogs" or "work-log" =>
                IntegrationLocalEntityTypes.TaskWorkLog,
            IntegrationLocalEntityTypes.TaskAttachment or "attachment" or "attachments" =>
                IntegrationLocalEntityTypes.TaskAttachment,
            _ => null,
        };
    }

    private static string ToExternalType(string localType) =>
        localType switch
        {
            IntegrationLocalEntityTypes.Task => IntegrationExternalEntityTypes.Issue,
            IntegrationLocalEntityTypes.ProjectUser => IntegrationExternalEntityTypes.User,
            IntegrationLocalEntityTypes.TaskComment => IntegrationExternalEntityTypes.Comment,
            IntegrationLocalEntityTypes.TaskWorkLog => IntegrationExternalEntityTypes.Worklog,
            IntegrationLocalEntityTypes.TaskAttachment => IntegrationExternalEntityTypes.Attachment,
            _ => localType,
        };

    private static IntegrationMappingStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IntegrationMappingStatus.Active;
        }

        return Enum.TryParse<IntegrationMappingStatus>(value.Trim(), ignoreCase: true, out var status)
            ? status
            : IntegrationMappingStatus.Active;
    }

    private static string FormatUserName(User user)
    {
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(full) ? user.Email : full;
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..(max - 1)] + "…";
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return WebUtility.HtmlDecode(
            System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " "))
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
