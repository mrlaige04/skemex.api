using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Queries.Notifications;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<Notification> notificationRepository)
    : ICommandHandler<MarkNotificationReadCommand, NotificationDto>
{
    public async Task<ErrorOr<NotificationDto>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId is null)
        {
            return Error.Unauthorized("Auth.Required", "Sign in to update notifications.");
        }

        var notification = await notificationRepository.GetAsync(
            filter: entry => entry.Id == request.NotificationId && entry.UserId == userId.Value,
            cancellationToken: cancellationToken);

        if (notification is null)
        {
            return Error.NotFound("Notification.NotFound", "Notification was not found.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await notificationRepository.UpdateAsync(notification, cancellationToken);
        }

        return NotificationMappings.ToDto(notification);
    }
}
