using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AiChatService(
    IAiProviderResolver providerResolver,
    IAiModelCatalogService modelCatalog,
    IBaseRepository<AiModel> modelRepository,
    IOptions<AiOptions> aiOptions) : IAiChatService
{
    public async Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException(
                "No AI model selected. Set a default model in project AI settings, or pick a model for this chat.");
        }

        var modelExternalId = request.Model.Trim();
        var providerName = await ResolveProviderNameAsync(modelExternalId, cancellationToken)
            .ConfigureAwait(false);
        var provider = await providerResolver
            .GetRequiredAsync(providerName, cancellationToken)
            .ConfigureAwait(false);

        return await provider
            .CompleteAsync(
                new AiChatRequest
                {
                    SystemPrompt = request.SystemPrompt,
                    UserPrompt = request.UserPrompt,
                    Model = modelExternalId,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        modelCatalog.ListAsync(forceRefresh, cancellationToken);

    private async Task<string> ResolveProviderNameAsync(
        string modelExternalId,
        CancellationToken cancellationToken)
    {
        var catalogEntry = await modelRepository
            .GetAsync(
                filter: model =>
                    model.ExternalId == modelExternalId && model.IsActive,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(catalogEntry?.Provider))
        {
            return catalogEntry.Provider;
        }

        if (!string.IsNullOrWhiteSpace(aiOptions.Value.ActiveProvider))
        {
            return aiOptions.Value.ActiveProvider.Trim();
        }

        var active = await providerResolver.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return active.Name;
    }
}
