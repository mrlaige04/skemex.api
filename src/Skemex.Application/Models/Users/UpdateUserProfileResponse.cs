namespace Skemex.Application.Models.Users;

public sealed class UpdateUserProfileResponse
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}
