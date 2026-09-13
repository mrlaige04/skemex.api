using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Services.Projects;

public static class ProjectTaskDtoMapper
{
    public static ProjectTaskDto Map(
        ProjectTask task,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId = null)
    {
        return new ProjectTaskDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectColumnId = task.ProjectColumnId,
            ColumnKey = task.Column?.Key ?? string.Empty,
            ColumnTitle = task.Column?.Title ?? string.Empty,
            ParentId = task.ParentId,
            Code = task.Code,
            Type = task.Type.ToString(),
            Title = task.Title,
            Description = task.Description,
            AcceptanceCriteria = MapAcceptanceCriteria(task),
            TestCases = MapTestCases(task),
            OriginalEstimateMinutes = task.OriginalEstimateMinutes,
            RemainingEstimateMinutes = task.RemainingEstimateMinutes,
            StoryPoints = task.StoryPoints,
            SpentMinutes = task.SpentMinutes,
            WorkLogs = [],
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Assignee = task.Assignee is null ? null : MapUser(task.Assignee, avatarUrlsByBlobId),
            Reporter = MapUser(task.Reporter, avatarUrlsByBlobId),
            Subtasks = [],
        };
    }

    public static IReadOnlyList<ProjectTaskDto> MapRootsWithSubtasks(
        IReadOnlyList<ProjectTask> allTasks,
        Guid columnId,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId = null)
    {
        var childrenByParentId = allTasks
            .Where(task => task.ParentId is not null)
            .GroupBy(task => task.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var roots = allTasks
            .Where(task => task.ProjectColumnId == columnId && task.ParentId is null)
            .OrderBy(task => task.CreatedAt)
            .ThenBy(task => task.Title)
            .ThenBy(task => task.Code)
            .Select(task => MapWithSubtasksRecursive(task, childrenByParentId, avatarUrlsByBlobId))
            .ToList();

        return roots;
    }

    public static ProjectTaskDto MapWithSubtasksFromLookup(
        ProjectTask task,
        IReadOnlyDictionary<Guid, List<ProjectTask>> childrenByParentId,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId = null)
    {
        return MapWithSubtasksRecursive(task, childrenByParentId, avatarUrlsByBlobId);
    }

    public static async Task<IReadOnlyDictionary<string, string?>> LoadAvatarUrlsAsync(
        IEnumerable<ProjectTask> tasks,
        IUrlService urlService,
        CancellationToken cancellationToken = default)
    {
        var uniqueBlobIds = tasks
            .SelectMany(task => new[] { task.Assignee?.PhotoBlobId, task.Reporter?.PhotoBlobId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>()
            .ToList();

        if (uniqueBlobIds.Count == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        const int maxConcurrency = 16;
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var fetchTasks = uniqueBlobIds.Select(async blobId =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var url = await urlService
                    .GetUserProfilePictureUrlAsync(blobId, cancellationToken)
                    .ConfigureAwait(false);
                return (BlobId: blobId, Url: url);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
        return results.ToDictionary(r => r.BlobId, r => r.Url, StringComparer.Ordinal);
    }

    private static ProjectTaskDto MapWithSubtasksRecursive(
        ProjectTask task,
        IReadOnlyDictionary<Guid, List<ProjectTask>> childrenByParentId,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId)
    {
        var subtasks = childrenByParentId.TryGetValue(task.Id, out var children)
            ? children
                .OrderBy(child => child.CreatedAt)
                .ThenBy(child => child.Title)
                .ThenBy(child => child.Code)
                .Select(child => MapWithSubtasksRecursive(child, childrenByParentId, avatarUrlsByBlobId))
                .ToList()
            : [];

        return new ProjectTaskDto
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectColumnId = task.ProjectColumnId,
            ColumnKey = task.Column?.Key ?? string.Empty,
            ColumnTitle = task.Column?.Title ?? string.Empty,
            ParentId = task.ParentId,
            Code = task.Code,
            Type = task.Type.ToString(),
            Title = task.Title,
            Description = task.Description,
            AcceptanceCriteria = MapAcceptanceCriteria(task),
            TestCases = MapTestCases(task),
            OriginalEstimateMinutes = task.OriginalEstimateMinutes,
            RemainingEstimateMinutes = task.RemainingEstimateMinutes,
            StoryPoints = task.StoryPoints,
            SpentMinutes = task.SpentMinutes,
            WorkLogs = [],
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Assignee = task.Assignee is null ? null : MapUser(task.Assignee, avatarUrlsByBlobId),
            Reporter = MapUser(task.Reporter, avatarUrlsByBlobId),
            Subtasks = subtasks,
        };
    }

    private static IReadOnlyList<string> MapAcceptanceCriteria(ProjectTask task) =>
        (task.AcceptanceCriteria ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

    private static IReadOnlyList<ProjectTaskTestCaseDto> MapTestCases(ProjectTask task) =>
        (task.TestCases ?? [])
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.Description)
                || !string.IsNullOrWhiteSpace(item.ExpectedResult))
            .Select(item => new ProjectTaskTestCaseDto
            {
                Type = NormalizeTestCaseType(item.Type),
                Description = item.Description?.Trim() ?? string.Empty,
                ExpectedResult = item.ExpectedResult?.Trim() ?? string.Empty,
            })
            .ToList();

    private static string NormalizeTestCaseType(string? type) =>
        string.Equals(type?.Trim(), "negative", StringComparison.OrdinalIgnoreCase)
            ? "negative"
            : "positive";

    private static ProjectTaskUserDto MapUser(
        User user,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId)
    {
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(user.PhotoBlobId)
            && avatarUrlsByBlobId is not null
            && avatarUrlsByBlobId.TryGetValue(user.PhotoBlobId, out var resolved))
        {
            avatarUrl = resolved;
        }

        return new ProjectTaskUserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            AvatarUrl = avatarUrl,
        };
    }
}
