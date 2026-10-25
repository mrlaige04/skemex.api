using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Roles.DeleteTenantRole;

public sealed class DeleteTenantRoleCommand : ICommand
{
    public Guid RoleId { get; init; }
}
