using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AiService(
    IAiProviderResolver providerResolver,
    IAiModelCatalogService modelCatalog,
    IBaseRepository<AiModel> modelRepository,
    IOptions<AiOptions> aiOptions) : IAiService
{
    public async Task<AiCompletionResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        var modelExternalId = request.Model.Trim();
        var provider = await ResolveProviderAsync(modelExternalId, cancellationToken).ConfigureAwait(false);

        return await provider
            .CompleteAsync(
                new AiCompletionRequest
                {
                    SystemPrompt = request.SystemPrompt,
                    UserPrompt = request.UserPrompt,
                    Model = modelExternalId,
                    PreferJsonObject = request.PreferJsonObject,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AiFunctionCallResult> FunctionCallAsync(
        AiFunctionCallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);
        ArgumentNullException.ThrowIfNull(request.Tools);
        if (request.Tools.Count == 0)
        {
            throw new ArgumentException("At least one tool is required for function calling.", nameof(request));
        }

        var modelExternalId = request.Model.Trim();
        var provider = await ResolveProviderAsync(modelExternalId, cancellationToken).ConfigureAwait(false);

        return await provider
            .FunctionCallAsync(
                new AiFunctionCallRequest
                {
                    SystemPrompt = request.SystemPrompt,
                    UserPrompt = request.UserPrompt,
                    Model = modelExternalId,
                    Tools = request.Tools,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<AiModelDto>> ListModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default) =>
        modelCatalog.ListAsync(forceRefresh, cancellationToken);

    private async Task<IAiProvider> ResolveProviderAsync(
        string modelExternalId,
        CancellationToken cancellationToken)
    {
        var providerName = await ResolveProviderNameAsync(modelExternalId, cancellationToken)
            .ConfigureAwait(false);
        return await providerResolver
            .GetRequiredAsync(providerName, cancellationToken)
            .ConfigureAwait(false);
    }

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
