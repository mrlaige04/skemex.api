using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai.Providers;

public sealed class AiProviderResolver(
    IBaseRepository<AiProvider> providerRepository,
    IEncryptService encryptService,
    IHttpClientFactory httpClientFactory,
    IOptions<AiOptions> aiOptions,
    ILoggerFactory loggerFactory) : IAiProviderResolver
{
    public async Task<IAiProvider> GetRequiredAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new InvalidOperationException("AI provider name is required.");
        }

        var key = providerName.Trim();
        var entity = await providerRepository
            .GetAsync(
                filter: provider => provider.Key == key && provider.IsEnabled,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            throw new InvalidOperationException(
                $"AI provider '{key}' is not configured or is disabled.");
        }

        return CreateRuntimeProvider(entity);
    }

    public async Task<IAiProvider> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var configured = aiOptions.Value.ActiveProvider?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var byConfig = await providerRepository
                .GetAsync(
                    filter: provider => provider.Key == configured && provider.IsEnabled,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (byConfig is not null)
            {
                return CreateRuntimeProvider(byConfig);
            }
        }

        var firstEnabled = await providerRepository
            .GetAsync(
                filter: provider => provider.IsEnabled,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (firstEnabled is null)
        {
            throw new InvalidOperationException(
                "No enabled AI providers are configured. Add one in SuperAdmin → AI Providers.");
        }

        return CreateRuntimeProvider(firstEnabled);
    }

    public async Task<IReadOnlyList<IAiProvider>> GetAllEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await providerRepository
            .GetAllAsync(
                filter: provider => provider.IsEnabled,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return entities
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateRuntimeProvider)
            .ToList();
    }

    public async Task<IAiProvider?> TryGetByKeyAsync(
        string providerKey,
        bool requireEnabled = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return null;
        }

        var key = providerKey.Trim();
        var entity = await providerRepository
            .GetAsync(
                filter: provider =>
                    provider.Key == key && (!requireEnabled || provider.IsEnabled),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : CreateRuntimeProvider(entity);
    }

    private IAiProvider CreateRuntimeProvider(AiProvider entity)
    {
        string? apiKey = null;
        if (!string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted))
        {
            apiKey = encryptService.Unprotect(entity.ApiKeyEncrypted);
        }

        AiProviderAuthConfig auth;
        try
        {
            auth = AiProviderAuthSecrets.DecryptFromStorage(entity.AuthConfigJson, encryptService);
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger<AiProviderResolver>()
                .LogWarning(ex, "Failed to decrypt auth config for AI provider {Key}", entity.Key);
            auth = new AiProviderAuthConfig();
        }

        return new OpenAiCompatibleAiProvider(
            key: entity.Key,
            displayName: entity.Name,
            baseUrl: entity.BaseUrl,
            apiKey: apiKey,
            authConfig: auth,
            httpClientFactory: httpClientFactory,
            logger: loggerFactory.CreateLogger($"AiProvider.{entity.Key}"));
    }
}
