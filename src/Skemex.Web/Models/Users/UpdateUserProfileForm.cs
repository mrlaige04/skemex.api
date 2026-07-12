namespace Skemex.Web.Models.Users;

public sealed class UpdateUserProfileForm
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public IFormFile? Image { get; set; }
}
