namespace Skemex.Application.Models.Roles;

public sealed class RoleDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public IReadOnlyList<Guid> PermissionIds { get; init; } = [];
    public IReadOnlyList<string> PermissionNames { get; init; } = [];
}

public sealed class PermissionGroupDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<PermissionItemDto> Permissions { get; init; } = [];
}

public sealed class PermissionItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
}
