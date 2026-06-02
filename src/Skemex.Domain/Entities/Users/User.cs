using Microsoft.AspNetCore.Identity;
using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class User : IdentityUser<Guid>, IEntity<Guid>, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string? PhotoBlobId { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public User() { }

    public IList<TenantUser> Tenants { get; set; } = [];
    public IList<UserRole> UserRoles { get; set; } = [];
}