using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;

namespace Skemex.Application.Features.Commands.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadCommand : ICommand<NotificationDto>
{
    public Guid NotificationId { get; init; }
}
