using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;
using Skemex.Application.Services;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Projects.GetProjectUsers;

public sealed class GetProjectUsersQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<Project> projectRepository,
    ITenantRepository<ProjectUser> projectUserRepository,
    IUrlService urlService)
    : IQueryHandler<GetProjectUsersQuery, PaginatedList<ProjectUserDto>>
{
    public async Task<ErrorOr<PaginatedList<ProjectUserDto>>> Handle(
        GetProjectUsersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing projects.");
        }

        var projectExists = await projectRepository.ExistsAsync(
            p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (!projectExists)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<ProjectUser, bool>> filter = pu => pu.ProjectId == request.ProjectId;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = pu =>
                pu.ProjectId == request.ProjectId &&
                ((pu.User.Email != null && pu.User.Email.ToLower().Contains(term)) ||
                 pu.User.FirstName.ToLower().Contains(term) ||
                 pu.User.LastName.ToLower().Contains(term));
        }

        var paginated = await projectUserRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q
                .Include(pu => pu.User)
                .OrderBy(pu => pu.User.LastName)
                .ThenBy(pu => pu.User.FirstName),
            cancellationToken: cancellationToken);

        var avatarUrls = await LoadAvatarUrlsAsync(
            paginated.Items.Select(pu => pu.User.PhotoBlobId),
            cancellationToken);

        var items = paginated.Items
            .Select(pu =>
            {
                string? avatarUrl = null;
                if (!string.IsNullOrWhiteSpace(pu.User.PhotoBlobId))
                {
                    avatarUrls.TryGetValue(pu.User.PhotoBlobId, out avatarUrl);
                }

                return new ProjectUserDto
                {
                    Id = pu.User.Id,
                    Email = pu.User.Email ?? string.Empty,
                    FirstName = pu.User.FirstName,
                    LastName = pu.User.LastName,
                    AvatarUrl = avatarUrl,
                };
            })
            .ToList();

        return new PaginatedList<ProjectUserDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }

    private async Task<IReadOnlyDictionary<string, string?>> LoadAvatarUrlsAsync(
        IEnumerable<string?> blobIds,
        CancellationToken cancellationToken)
    {
        var uniqueBlobIds = blobIds
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
}
