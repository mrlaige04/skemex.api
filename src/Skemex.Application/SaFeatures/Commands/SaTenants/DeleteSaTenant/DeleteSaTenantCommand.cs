using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.DeleteSaTenant;

public sealed class DeleteSaTenantCommand : ICommand
{
    public Guid TenantId { get; init; }
}
