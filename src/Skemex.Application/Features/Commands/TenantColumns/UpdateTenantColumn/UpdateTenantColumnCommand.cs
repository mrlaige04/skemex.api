using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Commands.TenantColumns.UpdateTenantColumn;

public sealed class UpdateTenantColumnCommand : ICommand<TenantColumnDto>
{
    public Guid ColumnId { get; init; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsSortOrderForced { get; set; }
}
