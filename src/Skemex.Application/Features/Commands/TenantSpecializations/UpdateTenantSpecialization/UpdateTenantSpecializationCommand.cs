using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Commands.TenantSpecializations.UpdateTenantSpecialization;

public sealed class UpdateTenantSpecializationCommand : ICommand<TenantSpecializationDto>
{
    public Guid SpecializationId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? DefaultSkills { get; init; }
}
