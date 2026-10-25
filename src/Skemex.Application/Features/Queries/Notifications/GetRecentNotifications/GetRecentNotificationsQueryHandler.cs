using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Notifications.GetRecentNotifications;

public sealed class GetRecentNotificationsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Notification> notificationRepository)
    : IQueryHandler<GetRecentNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<ErrorOr<IReadOnlyList<NotificationDto>>> Handle(
        GetRecentNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Required", "Sign in to view notifications.");
        }

        var take = request.Take < 1 ? 10 : Math.Min(request.Take, 50);

        var query = await notificationRepository.GetQuery(cancellationToken);
        var entities = await query
            .AsNoTracking()
            .Where(notification => notification.UserId == userId.Value)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(NotificationMappings.ToDto).ToList();
    }
}
