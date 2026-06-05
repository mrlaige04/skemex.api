using Skemex.Domain.Entities.Users;

namespace Skemex.Application.Services;

public interface IAuthEmailService
{
    Task SendRegistrationGreetingAsync(User user, CancellationToken cancellationToken = default);

    Task SendTenantInvitationAsync(
        User user,
        Tenant tenant,
        string invitationToken,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default);
}
