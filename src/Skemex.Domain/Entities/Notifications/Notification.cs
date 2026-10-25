using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Enums;

namespace Skemex.Domain.Entities.Notifications;

/// <summary>User-scoped in-app notification (global across tenants).</summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }

    /// <summary>Optional in-app path (e.g. /PROJ/issues/ABC-1).</summary>
    public string? Href { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
