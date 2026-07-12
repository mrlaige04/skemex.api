namespace Skemex.Web.Models.TenantColumns;

public sealed class UpdateTenantColumnRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSortOrderForced { get; set; }
}
