using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;

namespace Skemex.Application.Features.Commands.Roles.CreateTenantRole;

public sealed class CreateTenantRoleCommand : ICommand<RoleDetailDto>
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<Guid> PermissionIds { get; set; } = [];
}
