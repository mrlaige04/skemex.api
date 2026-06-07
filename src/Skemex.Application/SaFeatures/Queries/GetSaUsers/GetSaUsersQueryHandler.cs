using System.Linq.Expressions;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.GetSaUsers;

public sealed class GetSaUsersQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<User> userRepository,
    IProfileImageService profileImages,
    IOptions<SuperAdminOptions> superAdminOptions)
    : IQueryHandler<GetSaUsersQuery, PaginatedList<SaUserDto>>
{
    public async Task<ErrorOr<PaginatedList<SaUserDto>>> Handle(
        GetSaUsersQuery request,
        CancellationToken cancellationToken)
    {
        var access = SaSuperAdminContext.RequireSuperAdmin(currentUser);
        if (access.IsError)
        {
            return access.Errors;
        }

        var search = request.Search?.Trim();
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        var superAdminEmail = superAdminOptions.Value.Email.Trim().ToLowerInvariant();
        var hasSuperAdminEmail = superAdminEmail.Length > 0;
        var term = search?.ToLowerInvariant() ?? string.Empty;
        var hasSearch = term.Length > 0;

        Expression<Func<User, bool>> filter = u =>
            (!hasSuperAdminEmail || u.Email == null || u.Email.ToLower() != superAdminEmail) &&
            !u.UserRoles.Any(ur => ur.TenantId == null && ur.Role.Name == RoleNames.SuperAdmin) &&
            (!hasSearch ||
             (u.Email != null && u.Email.ToLower().Contains(term)) ||
             u.FirstName.ToLower().Contains(term) ||
             u.LastName.ToLower().Contains(term));

        var paginated = await userRepository.GetAllPaginatedAsync(
            pageNumber,
            pageSize,
            filter: filter,
            include: q => q
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .Include(u => u.Tenants)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName),
            cancellationToken: cancellationToken);

        var avatarUrls = await LoadAvatarUrlsAsync(
            paginated.Items.Select(u => u.PhotoBlobId),
            cancellationToken);

        var items = paginated.Items
            .Select(u => ToDto(u, avatarUrls))
            .ToList();

        return new PaginatedList<SaUserDto>(
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

    private static SaUserDto ToDto(User user, IReadOnlyDictionary<string, string?> avatarUrls)
    {
        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(user.PhotoBlobId))
        {
            avatarUrls.TryGetValue(user.PhotoBlobId, out avatarUrl);
        }

        return new SaUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CreatedAt = user.CreatedAt,
            AvatarUrl = avatarUrl,
            WorkspaceCount = user.Tenants.Count,
        };
    }
}
