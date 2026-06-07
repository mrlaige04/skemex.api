namespace Skemex.Application.SaFeatures;

public sealed class SaTenantDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string? LogoUrl { get; init; }

    public int MemberCount { get; init; }
}
