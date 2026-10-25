namespace Skemex.Application.Models.Users;

public sealed class TenantRoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
