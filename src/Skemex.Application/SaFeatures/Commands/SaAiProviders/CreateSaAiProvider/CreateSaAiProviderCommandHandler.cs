using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
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
using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;
using Skemex.Application.Helpers.Ai;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.CreateSaAiProvider;

public sealed class CreateSaAiProviderCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IEncryptService encryptService,
    IAiModelCatalogService modelCatalog)
    : ICommandHandler<CreateSaAiProviderCommand, SaAiProviderDto>
{
    public async Task<ErrorOr<SaAiProviderDto>> Handle(
        CreateSaAiProviderCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var key = request.Key.Trim();
        if (await providerRepository.ExistsAsync(
                filter: provider => provider.Key == key,
                cancellationToken: cancellationToken))
        {
            return Error.Conflict("AiProvider.KeyTaken", "An AI provider with this key already exists.");
        }

        var authPlain = SaAiProviderMapper.ToAuthConfig(request.AuthEntries);
        var entity = new AiProvider
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Key = key,
            BaseUrl = request.BaseUrl.Trim().TrimEnd('/'),
            IsEnabled = request.IsEnabled,
            ApiKeyEncrypted = string.IsNullOrWhiteSpace(request.ApiKey)
                ? null
                : encryptService.Protect(request.ApiKey.Trim()),
            AuthConfigJson = AiProviderAuthSecretsHelper.EncryptForStorage(authPlain, encryptService),
        };

        await providerRepository.AddAsync(entity, cancellationToken);

        try
        {
            await modelCatalog.SyncProviderByKeyAsync(entity.Key, cancellationToken: cancellationToken);
        }
        catch
        {
            // Provider is saved; catalog sync can be retried later via list refresh.
        }

        return SaAiProviderMapper.ToDto(entity);
    }
}
