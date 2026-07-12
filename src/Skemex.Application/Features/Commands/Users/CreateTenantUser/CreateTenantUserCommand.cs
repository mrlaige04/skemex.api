using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;
using Skemex.Domain.Consts;

namespace Skemex.Application.Features.Commands.Users.CreateTenantUser;

public sealed class CreateTenantUserCommand : ICommand<TenantUserDto>
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string RoleName { get; set; } = RoleNames.User;
}
