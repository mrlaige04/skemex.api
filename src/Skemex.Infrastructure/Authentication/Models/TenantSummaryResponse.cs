using Skemex.Domain.Entities.Users;
namespace Skemex.Infrastructure.Authentication.Models;


public class TenantSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public TenantUserStatus Status { get; set; } = TenantUserStatus.Active;
}
