using Microsoft.Extensions.Logging;
using Skemex.Application.Events.Abstractions;
using Skemex.Application.Models.Notifications;
using Skemex.Application.Services.Notifications;
using Skemex.Domain.Enums;
using Skemex.Domain.Events.Users;

namespace Skemex.Application.Events.Users;

public sealed class TenantUserInvitationAcceptedNotificationHandler(
    INotificationPublisher notificationPublisher,
    ILogger<TenantUserInvitationAcceptedNotificationHandler> logger)
    : IDomainEventHandler<TenantUserInvitationAcceptedDomainEvent>
{
    public async Task Handle(
        TenantUserInvitationAcceptedDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantName = string.IsNullOrWhiteSpace(domainEvent.TenantName)
                ? "the workspace"
                : domainEvent.TenantName.Trim();

            await notificationPublisher.PublishAsync(
                new NotificationPublishRequest
                {
                    UserId = domainEvent.UserId,
                    Severity = NotificationSeverity.Success,
                    Title = $"Welcome to {tenantName}",
                    Body = $"Thanks for joining {tenantName}. You're all set to get started.",
                    Href = "/dashboard",
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to publish join notification for tenant user {TenantUserId}.",
                domainEvent.TenantUserId);
        }
    }
}
