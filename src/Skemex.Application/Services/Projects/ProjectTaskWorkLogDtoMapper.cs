using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Services.Projects;

public static class ProjectTaskWorkLogDtoMapper
{
    public static ProjectTaskWorkLogDto Map(
        ProjectTaskWorkLog entry,
        IReadOnlyDictionary<string, string?>? avatarUrlsByBlobId = null) =>
        new()
        {
            Id = entry.Id,
            TaskId = entry.TaskId,
            ProjectId = entry.ProjectId,
            SpentMinutes = entry.SpentMinutes,
            StartedAt = entry.StartedAt,
            EndedAt = entry.EndedAt,
            Comment = entry.Comment,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt,
            User = MapUser(entry.User, avatarUrlsByBlobId),
        };

    public static async Task<IReadOnlyDictionary<string, string?>> LoadAvatarUrlsAsync(
        IEnumerable<ProjectTaskWorkLog> entries,
        IUrlService urlService,
        CancellationToken cancellationToken = default)
    {
        var uniqueBlobIds = entries
            .Select(entry => entry.User?.PhotoBlobId)
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
