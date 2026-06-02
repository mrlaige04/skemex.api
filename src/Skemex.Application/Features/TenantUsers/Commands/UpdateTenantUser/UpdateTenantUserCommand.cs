using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Commands.UpdateTenantUser;

public sealed class UpdateTenantUserCommand : ICommand<TenantUserDto>
{
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RoleName { get; set; }
}
