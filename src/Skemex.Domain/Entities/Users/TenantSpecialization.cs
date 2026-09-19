using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Users;

public class TenantSpecialization : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> DefaultSkills { get; set; } = [];

    public Tenant Tenant { get; set; } = null!;
    public ICollection<TenantUserSpecialization> Users { get; set; } = [];
}
