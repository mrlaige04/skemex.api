namespace Skemex.Application.SaFeatures;

public sealed class SaUserDto
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string? AvatarUrl { get; init; }

    public int WorkspaceCount { get; init; }
}
