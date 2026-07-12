using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Commands.Users.DeleteTenantUser;

public sealed class DeleteTenantUserCommand : ICommand
{
    public Guid UserId { get; set; }
}
