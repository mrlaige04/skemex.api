using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class RequestPasswordResetCommand : ICommand<RequestPasswordResetResponse>
{
    public string Email { get; set; } = string.Empty;
}
