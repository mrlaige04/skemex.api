using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaTenant;

public sealed class DeleteSaTenantCommand : ICommand
{
    public Guid TenantId { get; init; }
}
