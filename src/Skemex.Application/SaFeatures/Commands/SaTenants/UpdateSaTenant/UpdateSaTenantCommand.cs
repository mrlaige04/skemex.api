using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaTenants;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.UpdateSaTenant;

public sealed class UpdateSaTenantCommand : ICommand<SaTenantDto>
{
    public Guid TenantId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }
}
