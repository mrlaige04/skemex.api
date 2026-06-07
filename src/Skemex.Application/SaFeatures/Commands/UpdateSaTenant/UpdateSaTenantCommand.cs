using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.UpdateSaTenant;

public sealed class UpdateSaTenantCommand : ICommand<SaTenantDto>
{
    public Guid TenantId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }
}
