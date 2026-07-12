using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Application.Services;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Users.GetTenantUsers;

public sealed class GetTenantUsersQueryHandler(
    ICurrentUser currentUser,
    ITenantRepository<TenantUser> tenantUserRepository,
    IBaseRepository<UserRole> userRoleRepository,
    IProfileImageService profileImages)
    : IQueryHandler<GetTenantUsersQuery, PaginatedList<TenantUserDto>>
{
    public async Task<ErrorOr<PaginatedList<TenantUserDto>>> Handle(
        GetTenantUsersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        if (tenantId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing users.");
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        Expression<Func<TenantUser, bool>>? filter = null;
        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            filter = tu =>
                (tu.User.Email != null && tu.User.Email.ToLower().Contains(term)) ||
                tu.User.FirstName.ToLower().Contains(term) ||
                tu.User.LastName.ToLower().Contains(term);
        }

        var paginated = await tenantUserRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q
                .Include(tu => tu.User)
                .OrderBy(tu => tu.User.LastName)
                .ThenBy(tu => tu.User.FirstName),
            cancellationToken: cancellationToken);

        var userIds = paginated.Items.Select(tu => tu.UserId).ToHashSet();
        var rolesTask = LoadRolesByUserAsync(userRoleRepository, tenantId.Value, userIds, cancellationToken);
        var avatarsTask = LoadAvatarUrlsByBlobIdAsync(
            paginated.Items.Select(tu => tu.User.PhotoBlobId),
            cancellationToken);

        await Task.WhenAll(rolesTask, avatarsTask).ConfigureAwait(false);

        var rolesByUser = await rolesTask.ConfigureAwait(false);
        var avatarUrlsByBlobId = await avatarsTask.ConfigureAwait(false);

        var items = paginated.Items
            .Select(tu =>
            {
                var user = tu.User;
                rolesByUser.TryGetValue(user.Id, out var roles);

                string? avatarUrl = null;
                if (!string.IsNullOrWhiteSpace(user.PhotoBlobId))
                {
                    avatarUrlsByBlobId.TryGetValue(user.PhotoBlobId, out avatarUrl);
                }

                return new TenantUserDto
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    CreatedAt = user.CreatedAt,
                    Roles = roles ?? [],
                    Status = tu.Status,
                    AvatarUrl = avatarUrl,
                };
            })
            .ToList();

        return new PaginatedList<TenantUserDto>(
            items,
            paginated.TotalItems,
            paginated.PageNumber,
            paginated.PageSize);
    }

    private static async Task<Dictionary<Guid, List<string>>> LoadRolesByUserAsync(
        IBaseRepository<UserRole> userRoleRepository,
        Guid tenantId,
        HashSet<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        var userRoles = await userRoleRepository.GetAllAsync(
            filter: ur => ur.TenantId == tenantId && userIds.Contains(ur.UserId),
            include: q => q.Include(ur => ur.Role),
            cancellationToken: cancellationToken);

        return userRoles
            .GroupBy(ur => ur.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(ur => ur.Role.Name).Where(n => n is not null).Cast<string>().Distinct().OrderBy(n => n).ToList());
    }

    private async Task<IReadOnlyDictionary<string, string?>> LoadAvatarUrlsByBlobIdAsync(
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
                var url = await profileImages.GetAvatarUrlAsync(blobId, cancellationToken).ConfigureAwait(false);
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
