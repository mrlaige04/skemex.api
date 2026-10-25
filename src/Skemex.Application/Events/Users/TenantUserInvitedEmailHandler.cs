using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Skemex.Application.Events.Abstractions;
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Domain.Entities.Users;
using Skemex.Domain.Events.Users;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Application.Events.Users;

public sealed class TenantUserInvitedEmailHandler(
    UserManager<User> userManager,
    IBaseRepository<Tenant> tenantRepository,
    IAuthEmailService authEmailService,
    ILogger<TenantUserInvitedEmailHandler> logger)
    : IDomainEventHandler<TenantUserInvitedDomainEvent>
{
    public async Task Handle(
        TenantUserInvitedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await userManager.FindByIdAsync(domainEvent.UserId.ToString());
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
            {
                logger.LogWarning(
                    "Skipping invitation email for tenant user {TenantUserId}: user {UserId} was not found.",
                    domainEvent.TenantUserId,
                    domainEvent.UserId);
                return;
            }

            var tenant = await tenantRepository.GetAsync(
                filter: entry => entry.Id == domainEvent.TenantId,
                cancellationToken: cancellationToken);
            if (tenant is null)
            {
                logger.LogWarning(
                    "Skipping invitation email for tenant user {TenantUserId}: tenant {TenantId} was not found.",
                    domainEvent.TenantUserId,
                    domainEvent.TenantId);
                return;
            }

            await authEmailService
                .SendTenantInvitationAsync(
                    user,
                    tenant,
                    domainEvent.InvitationToken,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "User {UserId} was invited to tenant {TenantId}, but the invitation email was not sent.",
                domainEvent.UserId,
                domainEvent.TenantId);
        }
    }
}
