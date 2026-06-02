namespace Skemex.Application.Features.TenantUsers;

public sealed class TenantUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
}

public sealed class TenantRoleDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
