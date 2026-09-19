using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Commands.TenantSpecializations.CreateTenantSpecialization;

public sealed class CreateTenantSpecializationCommand : ICommand<TenantSpecializationDto>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string>? DefaultSkills { get; set; }
}
