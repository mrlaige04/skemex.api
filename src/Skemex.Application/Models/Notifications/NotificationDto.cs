namespace Skemex.Application.Models.Notifications;

public sealed class NotificationDto
{
    public Guid Id { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public string? Href { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
