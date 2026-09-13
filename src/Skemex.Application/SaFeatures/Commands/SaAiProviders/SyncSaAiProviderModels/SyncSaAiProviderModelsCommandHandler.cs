using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.SyncSaAiProviderModels;

public sealed class SyncSaAiProviderModelsCommandHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IBaseRepository<AiModel> modelRepository,
    IAiModelCatalogService modelCatalog)
    : ICommandHandler<SyncSaAiProviderModelsCommand, IReadOnlyList<SaAiProviderModelDto>>
{
    public async Task<ErrorOr<IReadOnlyList<SaAiProviderModelDto>>> Handle(
        SyncSaAiProviderModelsCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var provider = await providerRepository.GetAsync(
            filter: entry => entry.Id == request.ProviderId,
            cancellationToken: cancellationToken);

        if (provider is null)
        {
            return Error.NotFound("AiProvider.NotFound", "AI provider was not found.");
        }

        try
        {
            await modelCatalog.SyncProviderByKeyAsync(
                provider.Key,
                replaceExisting: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Error.Failure(
                "AiProvider.ModelsSyncFailed",
                $"Could not refetch models from the provider: {ex.Message}");
        }

        var models = await modelRepository.GetAllAsync(
            filter: model => model.Provider == provider.Key,
            cancellationToken: cancellationToken);

        return models
            .OrderByDescending(model => model.IsActive)
            .ThenBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(SaAiProviderModelDto.FromEntity)
            .ToList();
    }
}
