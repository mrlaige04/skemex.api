namespace Skemex.Application.Features.Commands.Users.UpdateUserProfile;

public sealed class UpdateUserProfileResponse
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}
