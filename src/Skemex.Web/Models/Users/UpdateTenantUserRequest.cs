namespace Skemex.Web.Models.Users;

public sealed class UpdateTenantUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RoleName { get; set; }
}
