namespace Skemex.Web.Models.TenantColumns;

public sealed class ReorderTenantColumnsRequest
{
    public IReadOnlyList<Guid> ColumnIds { get; set; } = [];
}
