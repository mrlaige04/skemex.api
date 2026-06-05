namespace Skemex.Infrastructure.Authentication.Models;

using Skemex.Domain.Entities.Users;

public class TenantSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public TenantUserStatus Status { get; set; } = TenantUserStatus.Active;
}
