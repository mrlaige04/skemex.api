namespace Skemex.Infrastructure.Authentication.Models;

public class TenantSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Public URL for the logo (from <c>LogoBlobId</c> and <c>Storage:PublicBlobBaseUrl</c>), if configured.</summary>
    public string? LogoUrl { get; set; }
}
