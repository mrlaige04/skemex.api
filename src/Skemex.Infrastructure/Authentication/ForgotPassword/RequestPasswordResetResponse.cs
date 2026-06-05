namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class RequestPasswordResetResponse
{
    public string Message { get; init; } =
        "If an account exists for this email, a reset code has been sent.";
}
