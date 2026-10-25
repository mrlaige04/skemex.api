using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Notifications.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<Notification> notificationRepository)
    : IQueryHandler<GetUnreadNotificationCountQuery, UnreadNotificationCountDto>
{
    public async Task<ErrorOr<UnreadNotificationCountDto>> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Required", "Sign in to view notifications.");
        }

        var query = await notificationRepository.GetQuery(cancellationToken);
        var count = await query
            .AsNoTracking()
            .CountAsync(
                notification => notification.UserId == userId.Value && !notification.IsRead,
                cancellationToken)
            .ConfigureAwait(false);

        return new UnreadNotificationCountDto { Count = count };
    }
}
