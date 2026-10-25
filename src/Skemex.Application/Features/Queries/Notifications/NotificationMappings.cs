using Skemex.Application.Models.Notifications;
using Skemex.Domain.Entities.Notifications;

namespace Skemex.Application.Features.Queries.Notifications;

internal static class NotificationMappings
{
    public static NotificationDto ToDto(Notification notification) =>
        new()
        {
            Id = notification.Id,
            Severity = notification.Severity.ToString(),
            Title = notification.Title,
            Body = notification.Body,
            Href = notification.Href,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
        };
}
