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

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProvider;

public sealed class UpdateSaAiProviderCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IBaseRepository<AiModel> modelRepository,
    IEncryptService encryptService,
    IAiModelCatalogService modelCatalog)
    : ICommandHandler<UpdateSaAiProviderCommand, SaAiProviderDto>
{
    public async Task<ErrorOr<SaAiProviderDto>> Handle(
        UpdateSaAiProviderCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var entity = await providerRepository.GetAsync(
            filter: provider => provider.Id == request.ProviderId,
            cancellationToken: cancellationToken);

        if (entity is null)
        {
            return Error.NotFound("AiProvider.NotFound", "AI provider was not found.");
        }

        var previousName = entity.Name;
        var previousBaseUrl = entity.BaseUrl;
        var previousAuth = entity.AuthConfigJson;
        var previousEnabled = entity.IsEnabled;

        entity.Name = request.Name.Trim();
        entity.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
        entity.IsEnabled = request.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            entity.ApiKeyEncrypted = encryptService.Protect(request.ApiKey.Trim());
        }

        var authPlain = SaAiProviderMapper.ToAuthConfig(request.AuthEntries);
        entity.AuthConfigJson = AiProviderAuthSecretsHelper.MergeForUpdate(
            authPlain,
            previousAuth,
            encryptService);

        await providerRepository.UpdateAsync(entity, cancellationToken);

        if (!string.Equals(previousName, entity.Name, StringComparison.Ordinal))
        {
            var models = await modelRepository.GetAllAsync(
                filter: model => model.Provider == entity.Key,
                cancellationToken: cancellationToken);

            foreach (var model in models)
            {
                model.ProviderName = entity.Name;
                model.UpdatedAt = DateTime.UtcNow;
                await modelRepository.UpdateAsync(model, cancellationToken);
            }
        }

        var urlOrAuthChanged =
            !string.Equals(previousBaseUrl, entity.BaseUrl, StringComparison.Ordinal)
            || !string.Equals(previousAuth, entity.AuthConfigJson, StringComparison.Ordinal);

        var reenabled = !previousEnabled && entity.IsEnabled;

        if (entity.IsEnabled && (urlOrAuthChanged || reenabled))
        {
            try
            {
                await modelCatalog.SyncProviderByKeyAsync(
                    entity.Key,
                    replaceExisting: urlOrAuthChanged,
                    cancellationToken);
            }
            catch
            {
                // Provider is updated; catalog sync can be retried later.
            }
        }

        return SaAiProviderMapper.ToDto(entity);
    }
}
