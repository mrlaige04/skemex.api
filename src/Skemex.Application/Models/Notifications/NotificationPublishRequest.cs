using Skemex.Domain.Enums;

namespace Skemex.Application.Models.Notifications;

public sealed class NotificationPublishRequest
{
    public required Guid UserId { get; init; }
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? Href { get; init; }
}
