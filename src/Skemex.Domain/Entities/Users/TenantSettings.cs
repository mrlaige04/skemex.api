using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Domain.Entities.Users;

/// <summary>1–1 workspace settings for a <see cref="Tenant"/>.</summary>
public class TenantSettings : TenantEntity
{
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Default AI configuration copied into new projects.</summary>
    public AiSettings AiSettings { get; set; } = new();
}
