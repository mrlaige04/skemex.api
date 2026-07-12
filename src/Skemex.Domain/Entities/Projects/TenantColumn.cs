using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Users;

namespace Skemex.Domain.Entities.Projects;

public class TenantColumn : TenantEntity
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSortOrderForced { get; set; }

    public Tenant Tenant { get; set; } = null!;
}