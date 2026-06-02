using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Commands.DeleteTenantUser;

public sealed class DeleteTenantUserCommand : ICommand
{
    public Guid UserId { get; set; }
}
