using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Notifications.GetNotifications;

public sealed class GetNotificationsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Notification> notificationRepository)
    : IQueryHandler<GetNotificationsQuery, PaginatedList<NotificationDto>>
{
    public async Task<ErrorOr<PaginatedList<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Required", "Sign in to view notifications.");
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);
        var search = request.Search?.Trim();

        var query = await notificationRepository.GetQuery(cancellationToken);
        query = query
            .AsNoTracking()
            .Where(notification => notification.UserId == userId.Value);

        if (request.Severity is { } severity)
        {
            query = query.Where(notification => notification.Severity == severity);
        }

        if (request.IsRead is { } isRead)
        {
            query = query.Where(notification => notification.IsRead == isRead);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(notification =>
                notification.Title.ToLower().Contains(term)
                || (notification.Body != null && notification.Body.ToLower().Contains(term)));
        }

        query = (request.Sort ?? NotificationSort.CreatedAtDesc) switch
        {
            NotificationSort.CreatedAtAsc =>
                query.OrderBy(notification => notification.CreatedAt).ThenBy(notification => notification.Id),
            _ => query.OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id),
        };

        var totalItems = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PaginatedList<NotificationDto>(
            entities.Select(NotificationMappings.ToDto).ToList(),
            totalItems,
            pageNumber,
            pageSize);
    }
}
