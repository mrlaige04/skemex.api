using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;

namespace Skemex.Application.Features.Queries.Notifications.GetRecentNotifications;

public sealed class GetRecentNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>>
{
    public int Take { get; init; } = 10;
}
