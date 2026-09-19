using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.TenantSpecializations.DeleteTenantSpecialization;

public sealed class DeleteTenantSpecializationCommand : ICommand
{
    public Guid SpecializationId { get; init; }
}
