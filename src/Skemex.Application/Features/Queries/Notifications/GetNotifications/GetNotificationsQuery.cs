using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Abstractions;
using Skemex.Domain.Enums;

namespace Skemex.Application.Features.Queries.Notifications.GetNotifications;

public sealed class GetNotificationsQuery : IQuery<PaginatedList<NotificationDto>>
{
    public string? Search { get; init; }
    public NotificationSeverity? Severity { get; init; }
    public bool? IsRead { get; init; }
    public string Sort { get; init; } = NotificationSort.CreatedAtDesc;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
