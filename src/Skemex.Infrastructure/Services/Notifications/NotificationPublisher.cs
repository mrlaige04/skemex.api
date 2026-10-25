using Skemex.Application.Models.Notifications;
using Skemex.Application.Services.Notifications;
using Skemex.Domain.Entities.Notifications;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Notifications;

public sealed class NotificationPublisher(
    IBaseRepository<Notification> notificationRepository) : INotificationPublisher
{
    public async Task<Notification> PublishAsync(
        NotificationPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("Notification title is required.", nameof(request));
        }

        if (title.Length > 256)
        {
            title = title[..256];
        }

        var body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim();
        if (body is { Length: > 2000 })
        {
            body = body[..2000];
        }

        var href = string.IsNullOrWhiteSpace(request.Href) ? null : request.Href.Trim();
        if (href is { Length: > 1024 })
        {
            href = href[..1024];
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Severity = request.Severity,
            Title = title,
            Body = body,
            Href = href,
            IsRead = false,
        };

        await notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);
        return notification;
    }
}
