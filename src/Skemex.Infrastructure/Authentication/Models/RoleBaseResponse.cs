namespace Skemex.Infrastructure.Authentication.Models;

public class RoleBaseResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsSystem { get; set; }
}
