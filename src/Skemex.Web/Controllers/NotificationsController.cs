using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Features.Commands.Notifications.MarkAllNotificationsRead;
using Skemex.Application.Features.Commands.Notifications.MarkNotificationRead;
using Skemex.Application.Features.Queries.Notifications.GetNotifications;
using Skemex.Application.Features.Queries.Notifications.GetRecentNotifications;
using Skemex.Application.Features.Queries.Notifications.GetUnreadNotificationCount;
using Skemex.Application.Models.Notifications;
using Skemex.Domain.Enums;

namespace Skemex.Web.Controllers;

[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? severity,
        [FromQuery] bool? isRead,
        [FromQuery] string sort = NotificationSort.CreatedAtDesc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        NotificationSeverity? parsedSeverity = null;
        if (!string.IsNullOrWhiteSpace(severity)
            && Enum.TryParse<NotificationSeverity>(severity.Trim(), ignoreCase: true, out var value))
        {
            parsedSeverity = value;
        }

        var result = await sender.Send(
            new GetNotificationsQuery
            {
                Search = search,
                Severity = parsedSeverity,
                IsRead = isRead,
                Sort = sort,
                PageNumber = page,
                PageSize = pageSize,
            },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetUnreadNotificationCountQuery(), cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetRecentNotificationsQuery { Take = take },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new MarkNotificationReadCommand { NotificationId = id },
            cancellationToken);
        return result.Match(Ok, Problem);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
        return result.Match(Ok, Problem);
    }
}
