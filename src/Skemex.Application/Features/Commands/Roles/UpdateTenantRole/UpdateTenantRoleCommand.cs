using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;

namespace Skemex.Application.Features.Commands.Roles.UpdateTenantRole;

public sealed class UpdateTenantRoleCommand : ICommand<RoleDetailDto>
{
    public Guid RoleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<Guid> PermissionIds { get; set; } = [];
}
