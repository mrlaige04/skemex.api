using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Services;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Email;

public sealed class AuthEmailService(
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    ILogger<AuthEmailService> logger) : IAuthEmailService
{
    private const int InvitationExpiryDays = InvitationTokenGenerator.ExpiryDays;

    public Task SendRegistrationGreetingAsync(User user, CancellationToken cancellationToken = default)
    {
        var name = DisplayName(user);
        var subject = "Welcome to Skemex";
        var body = $"""
                    <p>Hi {name},</p>
                    <p>Your Skemex account has been created. You can sign in and create your first company whenever you are ready.</p>
                    <p><a href="{FrontendUrl("/auth/login")}">Sign in to Skemex</a></p>
                    <p>— The Skemex team</p>
                    """;

        return SendSafeAsync(user.Email!, subject, body, cancellationToken);
    }

    public Task SendTenantInvitationAsync(
        User user,
        Tenant tenant,
        string invitationToken,
        CancellationToken cancellationToken = default)
    {
        var name = DisplayName(user);
        var acceptUrl = FrontendUrl($"/invitations/accept?token={Uri.EscapeDataString(invitationToken)}");
        var subject = $"You have been invited to {tenant.Name} on Skemex";
        var body = $"""
                    <p>Hi {name},</p>
                    <p>You have been invited to join <strong>{tenant.Name}</strong> on Skemex.</p>
                    <p><a href="{acceptUrl}">Accept invitation</a></p>
                    <p>This link expires in {InvitationExpiryDays} days.</p>
                    <p>If you did not expect this invitation, you can ignore this email.</p>
                    <p>— The Skemex team</p>
                    """;

        return SendSafeAsync(user.Email!, subject, body, cancellationToken);
    }

    public Task SendPasswordResetCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default)
    {
        var name = DisplayName(user);
        var subject = "Your Skemex password reset code";
        var body = $"""
                    <p>Hi {name},</p>
                    <p>Use this code to reset your Skemex password:</p>
                    <p style="font-size: 24px; font-weight: 600; letter-spacing: 0.2em;">{code}</p>
                    <p>This code expires in 15 minutes.</p>
                    <p>If you did not request a password reset, you can ignore this email.</p>
                    <p>— The Skemex team</p>
                    """;

        return SendSafeAsync(user.Email!, subject, body, cancellationToken);
    }

    public static DateTimeOffset InvitationExpiresAt() =>
        DateTimeOffset.UtcNow.AddDays(InvitationExpiryDays);

    private async Task SendSafeAsync(
        string email,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendEmailAsync(email, subject, body, isHtml: true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send auth email to {Email} with subject {Subject}.", email, subject);
            throw;
        }
    }

    private string FrontendUrl(string path)
    {
        var baseUrl = appOptions.Value.FrontendBaseUrl.TrimEnd('/');
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return $"{baseUrl}{path}";
    }

    private static string DisplayName(User user)
    {
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return full.Length > 0 ? full : user.Email ?? "there";
    }
}
