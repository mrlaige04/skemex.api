namespace Skemex.Application.Features.Queries.Users.GetCurrentUserProfile;

public sealed class CurrentUserProfileResponse
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}
