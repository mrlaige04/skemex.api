using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skemex.Application.Helpers.Users;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Documents;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;
using Skemex.Infrastructure.Data;

namespace Skemex.Infrastructure.Services.Ai.Chat;

public sealed class AiChatArtefactResolver(
    SkemexDbContext dbContext,
    IProjectDocumentStorageService documentStorage,
    ILogger<AiChatArtefactResolver> logger) : IAiChatArtefactResolver
{
    public async Task<AiChatArtefactResolution> ResolveAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyList<AiChatArtefactRef> artefacts,
        CancellationToken cancellationToken = default)
    {
        if (artefacts.Count == 0)
        {
            return new AiChatArtefactResolution();
        }

        var ordered = artefacts
            .Where(item => item.Number > 0 && item.EntityId != Guid.Empty)
            .GroupBy(item => item.Number)
            .Select(group => group.First())
            .OrderBy(item => item.Number)
            .ToList();

        if (ordered.Count == 0)
        {
            return new AiChatArtefactResolution();
        }

        var taskIds = ordered
            .Where(item => IsType(item.Type, AiChatArtefactTypes.Task))
            .Select(item => item.EntityId)
            .Distinct()
            .ToList();
        var userIds = ordered
            .Where(item => IsType(item.Type, AiChatArtefactTypes.User))
            .Select(item => item.EntityId)
            .Distinct()
            .ToList();
        var documentIds = ordered
            .Where(item => IsType(item.Type, AiChatArtefactTypes.Document))
            .Select(item => item.EntityId)
            .Distinct()
            .ToList();

        var tasks = taskIds.Count == 0
            ? new Dictionary<Guid, ProjectTask>()
            : await dbContext.ProjectTasks.AsNoTracking()
                .Where(task =>
                    task.TenantId == tenantId
                    && task.ProjectId == projectId
                    && taskIds.Contains(task.Id))
                .ToDictionaryAsync(task => task.Id, cancellationToken)
                .ConfigureAwait(false);

        var users = userIds.Count == 0
            ? new Dictionary<Guid, UserArtefactRow>()
            : await LoadUsersAsync(tenantId, projectId, userIds, cancellationToken)
                .ConfigureAwait(false);

        var documents = documentIds.Count == 0
            ? new Dictionary<Guid, ProjectDocument>()
            : await dbContext.ProjectDocuments.AsNoTracking()
                .Where(doc =>
                    doc.TenantId == tenantId
                    && doc.ProjectId == projectId
                    && documentIds.Contains(doc.Id))
                .ToDictionaryAsync(doc => doc.Id, cancellationToken)
                .ConfigureAwait(false);

        var tableBuilder = new StringBuilder();
        var hasTableRows = false;
        tableBuilder.AppendLine("[ARTEFACTS: #|Type|Content]");

        foreach (var artefact in ordered)
        {
            if (IsType(artefact.Type, AiChatArtefactTypes.Task)
                && tasks.TryGetValue(artefact.EntityId, out var task))
            {
                hasTableRows = true;
                tableBuilder.Append(artefact.Number);
                tableBuilder.Append("|Task|");
                tableBuilder.Append("code=");
                tableBuilder.Append(Compact(task.Code));
                tableBuilder.Append("; title=");
                tableBuilder.Append(Compact(task.Title));
                tableBuilder.Append("; description=");
                tableBuilder.AppendLine(Compact(task.Description));
                continue;
            }

            if (IsType(artefact.Type, AiChatArtefactTypes.User)
                && users.TryGetValue(artefact.EntityId, out var user))
            {
                hasTableRows = true;
                tableBuilder.Append(artefact.Number);
                tableBuilder.Append("|User|");
                tableBuilder.Append("firstName=");
                tableBuilder.Append(Compact(user.FirstName));
                tableBuilder.Append("; lastName=");
                tableBuilder.Append(Compact(user.LastName));
                tableBuilder.Append("; specializations=");
                tableBuilder.Append(Compact(user.SpecializationTitles));
                tableBuilder.Append("; skills=");
                tableBuilder.AppendLine(Compact(user.Skills));
            }
        }

        var documentAttachments = new List<AiCompletionAttachment>();
        foreach (var artefact in ordered.Where(item => IsType(item.Type, AiChatArtefactTypes.Document)))
        {
            if (!documents.TryGetValue(artefact.EntityId, out var document)
                || string.IsNullOrWhiteSpace(document.BlobId))
            {
                continue;
            }

            try
            {
                await using var stream = await documentStorage
                    .OpenReadAsync(document.BlobId, cancellationToken)
                    .ConfigureAwait(false);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                documentAttachments.Add(
                    new AiCompletionAttachment
                    {
                        FileName = string.IsNullOrWhiteSpace(document.FileName)
                            ? $"artefact-{artefact.Number}"
                            : document.FileName,
                        ContentType = string.IsNullOrWhiteSpace(document.ContentType)
                            ? "application/octet-stream"
                            : document.ContentType,
                        Data = memory.ToArray(),
                    });
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to load document artefact {DocumentId} (#{Number}).",
                    document.Id,
                    artefact.Number);
            }
        }

        string? promptTable = null;
        if (hasTableRows)
        {
            promptTable = tableBuilder.ToString().TrimEnd();
            if (documentAttachments.Count > 0)
            {
                var docNumbers = string.Join(
                    ", ",
                    ordered
                        .Where(item =>
                            IsType(item.Type, AiChatArtefactTypes.Document)
                            && documents.ContainsKey(item.EntityId))
                        .Select(item => item.Number));
                promptTable =
                    $"{promptTable}{Environment.NewLine}{Environment.NewLine}"
                    + $"Document artefacts ({docNumbers}) are attached to this completion as files.";
            }
        }
        else if (documentAttachments.Count > 0)
        {
            var docNumbers = string.Join(
                ", ",
                ordered
                    .Where(item =>
                        IsType(item.Type, AiChatArtefactTypes.Document)
                        && documents.ContainsKey(item.EntityId))
                    .Select(item => item.Number));
            promptTable =
                $"Document artefacts ({docNumbers}) are attached to this completion as files.";
        }

        return new AiChatArtefactResolution
        {
            PromptTableMarkdown = promptTable,
            DocumentAttachments = documentAttachments,
        };
    }

    private async Task<Dictionary<Guid, UserArtefactRow>> LoadUsersAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from projectUser in dbContext.ProjectUsers.AsNoTracking()
                where projectUser.ProjectId == projectId
                    && projectUser.TenantId == tenantId
                    && userIds.Contains(projectUser.UserId)
                join tenantUser in dbContext.Set<TenantUser>().AsNoTracking()
                    on projectUser.UserId equals tenantUser.UserId
                where tenantUser.TenantId == tenantId
                    && tenantUser.Status == TenantUserStatus.Active
                join user in dbContext.Users.AsNoTracking()
                    on tenantUser.UserId equals user.Id
                select new
                {
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    tenantUser.Skills,
                    Specializations = tenantUser.Specializations
                        .Where(link => link.Specialization != null)
                        .Select(link => link.Specialization.Title)
                        .ToList(),
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.Id,
            row => new UserArtefactRow(
                row.FirstName,
                row.LastName,
                string.Join(
                    ", ",
                    row.Specializations
                        .Where(title => !string.IsNullOrWhiteSpace(title))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(title => title, StringComparer.OrdinalIgnoreCase)),
                string.Join(
                    ", ",
                    TenantUserSkillMergeHelper.NormalizeSkills(row.Skills ?? []))));
    }

    private static bool IsType(string? type, string expected) =>
        string.Equals(type?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static string Compact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Replace('|', '/')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private sealed record UserArtefactRow(
        string FirstName,
        string LastName,
        string SpecializationTitles,
        string Skills);
}
