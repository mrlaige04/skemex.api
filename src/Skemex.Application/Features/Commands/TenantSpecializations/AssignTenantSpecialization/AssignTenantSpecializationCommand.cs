using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Commands.TenantSpecializations.AssignTenantSpecialization;

public sealed class AssignTenantSpecializationCommand : ICommand<TenantUserDto>
{
    public Guid SpecializationId { get; init; }
    /// <summary>Application user id (<see cref="TenantUserDto.Id"/>), not the TenantUser row id.</summary>
    public Guid UserId { get; init; }
}
