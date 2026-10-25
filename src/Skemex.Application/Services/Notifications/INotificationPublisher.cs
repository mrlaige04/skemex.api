using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;

namespace Skemex.Application.Services.Notifications;

public interface INotificationPublisher
{
    Task<Notification> PublishAsync(
        NotificationPublishRequest request,
        CancellationToken cancellationToken = default);
}
