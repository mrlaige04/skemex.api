namespace Skemex.Domain.Entities.Users;

public class TenantUserSpecialization
{
    public Guid TenantUserId { get; set; }
    public TenantUser TenantUser { get; set; } = null!;

    public Guid TenantSpecializationId { get; set; }
    public TenantSpecialization Specialization { get; set; } = null!;
}
