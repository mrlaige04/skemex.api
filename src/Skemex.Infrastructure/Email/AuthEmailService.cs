using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
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
using Skemex.Domain.Enums;
using Skemex.Infrastructure.Authentication.Tokens;
using Skemex.Domain.Consts;

namespace Skemex.Infrastructure.Email;

public sealed class AuthEmailService(
    IEmailTemplateService emailTemplateService,
    IOptions<AppOptions> appOptions,
    IOptions<NumericEmailTokenProviderOptions> tokenOptions,
    ILogger<AuthEmailService> logger) : IAuthEmailService
{
    private const int InvitationExpiryDays = InvitationConsts.ExpiryDays;

    public Task SendRegistrationGreetingAsync(User user, CancellationToken cancellationToken = default)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = DisplayName(user),
            ["LoginUrl"] = FrontendUrl("/auth/login"),
        };

        return SendSafeAsync(
            EmailTemplateType.WelcomeSignup,
            tenantId: null,
            user.Email!,
            placeholders,
            cancellationToken);
    }

    public Task SendTenantInvitationAsync(
        User user,
        Tenant tenant,
        string invitationToken,
        CancellationToken cancellationToken = default)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = DisplayName(user),
            ["TenantName"] = tenant.Name,
            ["AcceptUrl"] = FrontendUrl($"/invitations/accept?token={Uri.EscapeDataString(invitationToken)}"),
            ["ExpiryDays"] = InvitationExpiryDays.ToString(),
        };

        return SendSafeAsync(
            EmailTemplateType.TenantInvite,
            tenant.Id,
            user.Email!,
            placeholders,
            cancellationToken);
    }

    public Task SendPasswordResetCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default)
    {
        var expiryMinutes = (int)tokenOptions.Value.TokenLifespan.TotalMinutes;
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Name"] = DisplayName(user),
            ["Code"] = code,
            ["ExpiryMinutes"] = expiryMinutes.ToString(),
        };

        return SendSafeAsync(
            EmailTemplateType.PasswordResetCode,
            tenantId: null,
            user.Email!,
            placeholders,
            cancellationToken);
    }

    public static DateTimeOffset InvitationExpiresAt() =>
        DateTimeOffset.UtcNow.AddDays(InvitationExpiryDays);

    private async Task SendSafeAsync(
        EmailTemplateType type,
        Guid? tenantId,
        string email,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailTemplateService
                .SendAsync(type, tenantId, email, placeholders, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send auth email {TemplateType} to {Email}.", type, email);
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
