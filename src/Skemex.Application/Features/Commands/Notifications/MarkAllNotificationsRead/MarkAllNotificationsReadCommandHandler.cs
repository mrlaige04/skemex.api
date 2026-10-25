using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Notification> notificationRepository)
    : ICommandHandler<MarkAllNotificationsReadCommand, MarkAllNotificationsReadResult>
{
    public async Task<ErrorOr<MarkAllNotificationsReadResult>> Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Required", "Sign in to update notifications.");
        }

        var unread = await notificationRepository.GetAllAsync(
            filter: notification => notification.UserId == userId.Value && !notification.IsRead,
            cancellationToken: cancellationToken);

        if (unread.Count == 0)
        {
            return new MarkAllNotificationsReadResult { UpdatedCount = 0 };
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await notificationRepository.UpdateRangeAsync(unread, cancellationToken);

        return new MarkAllNotificationsReadResult { UpdatedCount = unread.Count };
    }
}
