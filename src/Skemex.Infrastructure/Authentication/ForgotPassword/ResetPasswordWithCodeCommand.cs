using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class ResetPasswordWithCodeCommand : ICommand<ResetPasswordWithCodeResponse>
{
    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
