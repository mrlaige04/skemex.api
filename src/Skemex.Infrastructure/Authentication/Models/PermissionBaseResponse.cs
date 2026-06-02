namespace Skemex.Infrastructure.Authentication.Models;

public class PermissionBaseResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public bool IsSystem { get; set; }
}
