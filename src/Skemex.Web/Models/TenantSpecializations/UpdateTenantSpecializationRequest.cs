namespace Skemex.Web.Models.TenantSpecializations;

public sealed class UpdateTenantSpecializationRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<string>? DefaultSkills { get; set; }
}
