using ErrorOr;
using Skemex.Infrastructure.Authentication.Login;

namespace Skemex.Infrastructure.Authentication.External;

public interface IExternalAuthService
{
    IReadOnlyList<ExternalAuthProviderDto> ListConfiguredProviders();

    ErrorOr<Uri> BuildStartRedirect(string provider);

    /// <summary>Always returns a frontend redirect (success with code or error query).</summary>
    Task<Uri> HandleCallbackAsync(
        string provider,
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<LoginResponse>> ExchangeAsync(
        string code,
        CancellationToken cancellationToken = default);
}
