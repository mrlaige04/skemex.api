using System.Security.Cryptography;
using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Configuration.Ai;
using Skemex.Application.Configuration.App;
using Skemex.Application.Configuration.Documents;
using Skemex.Application.Configuration.Email;
using Skemex.Application.Configuration.Rag;
using Skemex.Application.Configuration.Storage;
using Skemex.Domain.Entities.Users;
using Skemex.Infrastructure.Authentication.Login;
using Skemex.Infrastructure.Authentication.Services;

namespace Skemex.Infrastructure.Authentication.External;

public sealed class ExternalAuthService(
    IEnumerable<IExternalAuthProvider> providers,
    IOptions<ExternalAuthOptions> externalOptions,
    IOptions<AppOptions> appOptions,
    IOptions<SuperAdminOptions> superAdminOptions,
    UserManager<User> userManager,
    AuthLoginSessionBuilder loginSessionBuilder,
    IMemoryCache cache,
    ILogger<ExternalAuthService> logger) : IExternalAuthService
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ExchangeTtl = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, IExternalAuthProvider> _providers = providers
        .ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ExternalAuthProviderDto> ListConfiguredProviders() =>
        _providers.Values
            .Where(provider => provider.IsConfigured)
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new ExternalAuthProviderDto
            {
                Name = provider.Name,
                DisplayName = provider.DisplayName,
            })
            .ToList();

    public ErrorOr<Uri> BuildStartRedirect(string provider)
    {
        if (!_providers.TryGetValue(provider, out var authProvider) || !authProvider.IsConfigured)
        {
            return Error.NotFound(
                ExternalAuthErrors.ProviderUnavailable,
                ExternalAuthErrors.ProviderUnavailableDescription);
        }

        var callbackBase = externalOptions.Value.CallbackBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(callbackBase))
        {
            return Error.Failure(
                ExternalAuthErrors.Misconfigured,
                "Auth:External:CallbackBaseUrl is not configured.");
        }

        var state = CreateOpaqueToken();
        cache.Set(
            StateCacheKey(state),
            new ExternalAuthState(authProvider.Name, DateTimeOffset.UtcNow),
            StateTtl);

        var redirectUri = $"{callbackBase}/api/auth/external/{authProvider.Name}/callback";
        return authProvider.BuildAuthorizationUrl(redirectUri, state);
    }

    public async Task<Uri> HandleCallbackAsync(
        string provider,
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default)
    {
        var frontendBase = appOptions.Value.FrontendBaseUrl.Trim().TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(error))
        {
            var message = string.IsNullOrWhiteSpace(errorDescription) ? error : errorDescription;
            return FrontendError(frontendBase, message!);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return FrontendError(frontendBase, "Missing authorization code or state.");
        }

        if (!cache.TryGetValue(StateCacheKey(state), out ExternalAuthState? authState)
            || authState is null
            || !string.Equals(authState.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            return FrontendError(frontendBase, "Invalid or expired OAuth state. Please try again.");
        }

        cache.Remove(StateCacheKey(state));

        if (!_providers.TryGetValue(provider, out var authProvider) || !authProvider.IsConfigured)
        {
            return FrontendError(frontendBase, "This sign-in provider is not available.");
        }

        var callbackBase = externalOptions.Value.CallbackBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(callbackBase))
        {
            return FrontendError(frontendBase, "OAuth callback base URL is not configured.");
        }

        var redirectUri = $"{callbackBase}/api/auth/external/{authProvider.Name}/callback";

        ExternalUserInfo info;
        try
        {
            info = await authProvider
                .ExchangeCodeAsync(code, redirectUri, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "External auth exchange failed for provider {Provider}.", provider);
            return FrontendError(frontendBase, ex.Message);
        }

        if (string.IsNullOrWhiteSpace(info.Email))
        {
            return FrontendError(
                frontendBase,
                "The provider did not return a verified email address.");
        }

        var userResult = await ResolveOrCreateUserAsync(info, cancellationToken).ConfigureAwait(false);
        if (userResult.IsError)
        {
            return FrontendError(frontendBase, userResult.FirstError.Description);
        }

        var exchangeCode = CreateOpaqueToken();
        cache.Set(ExchangeCacheKey(exchangeCode), userResult.Value.Id, ExchangeTtl);

        return new Uri($"{frontendBase}/auth/external/callback?code={Uri.EscapeDataString(exchangeCode)}");
    }

    public async Task<ErrorOr<LoginResponse>> ExchangeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Error.Validation(
                ExternalAuthErrors.InvalidExchangeCode,
                ExternalAuthErrors.InvalidExchangeCodeDescription);
        }

        var exchangeKey = ExchangeCacheKey(code.Trim());
        if (!cache.TryGetValue(exchangeKey, out Guid userId) || userId == Guid.Empty)
        {
            return Error.Unauthorized(
                ExternalAuthErrors.InvalidExchangeCode,
                ExternalAuthErrors.InvalidExchangeCodeDescription);
        }

        cache.Remove(exchangeKey);

        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null)
        {
            return Error.NotFound(UserErrors.NotFound, UserErrors.NotFoundDescription);
        }

        return await loginSessionBuilder.BuildAsync(user, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ErrorOr<User>> ResolveOrCreateUserAsync(
        ExternalUserInfo info,
        CancellationToken cancellationToken)
    {
        var existingByLogin = await userManager
            .FindByLoginAsync(info.Provider, info.Subject)
            .ConfigureAwait(false);
        if (existingByLogin is not null)
        {
            return existingByLogin;
        }

        var email = info.Email.Trim();
        var existingByEmail = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existingByEmail is not null)
        {
            var link = await userManager.AddLoginAsync(
                existingByEmail,
                new UserLoginInfo(info.Provider, info.Subject, info.Provider)).ConfigureAwait(false);
            if (!link.Succeeded && !link.Errors.Any(error =>
                    error.Code.Contains("LoginAlreadyAssociated", StringComparison.OrdinalIgnoreCase)))
            {
                var description = string.Join(' ', link.Errors.Select(error => error.Description));
                return Error.Failure(ExternalAuthErrors.LinkFailed, description);
            }

            if (!existingByEmail.EmailConfirmed)
            {
                existingByEmail.EmailConfirmed = true;
                await userManager.UpdateAsync(existingByEmail).ConfigureAwait(false);
            }

            return existingByEmail;
        }

        if (superAdminOptions.Value.MatchesEmail(email))
        {
            return Error.Validation(
                UserErrors.SuperAdminEmailReserved,
                UserErrors.SuperAdminEmailReservedDescription);
        }

        var firstName = string.IsNullOrWhiteSpace(info.FirstName) ? "User" : info.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(info.LastName) ? string.Empty : info.LastName.Trim();

        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = Truncate(firstName, 128),
            LastName = Truncate(lastName, 128),
            EmailConfirmed = true,
        };

        var create = await userManager.CreateAsync(user).ConfigureAwait(false);
        if (!create.Succeeded)
        {
            if (create.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                // Race: another request created the user — merge instead.
                var raced = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
                if (raced is not null)
                {
                    await userManager.AddLoginAsync(
                        raced,
                        new UserLoginInfo(info.Provider, info.Subject, info.Provider)).ConfigureAwait(false);
                    return raced;
                }
            }

            var description = string.Join(' ', create.Errors.Select(error => error.Description));
            return Error.Failure(ExternalAuthErrors.CreateFailed, description);
        }

        var addLogin = await userManager.AddLoginAsync(
            user,
            new UserLoginInfo(info.Provider, info.Subject, info.Provider)).ConfigureAwait(false);
        if (!addLogin.Succeeded)
        {
            var description = string.Join(' ', addLogin.Errors.Select(error => error.Description));
            return Error.Failure(ExternalAuthErrors.LinkFailed, description);
        }

        return user;
    }

    private static Uri FrontendError(string frontendBase, string message) =>
        new($"{frontendBase}/auth/external/callback?error={Uri.EscapeDataString(Truncate(message, 300))}");

    private static string StateCacheKey(string state) => $"ext-auth:state:{state}";

    private static string ExchangeCacheKey(string code) => $"ext-auth:exchange:{code}";

    private static string CreateOpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed record ExternalAuthState(string Provider, DateTimeOffset CreatedAt);
}

public static class ExternalAuthErrors
{
    public const string ProviderUnavailable = "Auth.ExternalProviderUnavailable";
    public const string ProviderUnavailableDescription = "This sign-in provider is not available.";

    public const string Misconfigured = "Auth.ExternalMisconfigured";

    public const string InvalidExchangeCode = "Auth.InvalidExchangeCode";
    public const string InvalidExchangeCodeDescription =
        "The sign-in code is invalid or has expired. Please try again.";

    public const string LinkFailed = "Auth.ExternalLinkFailed";
    public const string CreateFailed = "Auth.ExternalCreateFailed";
}
