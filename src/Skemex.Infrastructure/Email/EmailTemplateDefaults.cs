using Skemex.Domain.Enums;

namespace Skemex.Infrastructure.Email;

internal static class EmailTemplateDefaults
{
    public static string DefaultTitle(EmailTemplateType type) =>
        type switch
        {
            EmailTemplateType.WelcomeSignup => "Welcome signup",
            EmailTemplateType.TenantInvite => "Tenant invitation",
            EmailTemplateType.PasswordResetCode => "Password reset code",
            _ => type.ToString(),
        };

    public static string DefaultSubject(EmailTemplateType type) =>
        type switch
        {
            EmailTemplateType.WelcomeSignup => "Welcome to Skemex",
            EmailTemplateType.TenantInvite => "You have been invited to {{TenantName}} on Skemex",
            EmailTemplateType.PasswordResetCode => "Your Skemex password reset code",
            _ => "Skemex notification",
        };
}
