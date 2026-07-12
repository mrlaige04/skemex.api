namespace Skemex.Web.Models.Projects;

public sealed class ReorderProjectColumnsRequest
{
    public IReadOnlyList<Guid> ColumnIds { get; set; } = [];
}
