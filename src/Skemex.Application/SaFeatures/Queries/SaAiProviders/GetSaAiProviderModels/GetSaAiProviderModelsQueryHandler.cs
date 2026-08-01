using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderModels;

public sealed class GetSaAiProviderModelsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<AiProvider> providerRepository,
    IBaseRepository<AiModel> modelRepository)
    : IQueryHandler<GetSaAiProviderModelsQuery, IReadOnlyList<SaAiProviderModelDto>>
{
    public async Task<ErrorOr<IReadOnlyList<SaAiProviderModelDto>>> Handle(
        GetSaAiProviderModelsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var provider = await providerRepository.GetAsync(
            filter: p => p.Id == request.ProviderId,
            cancellationToken: cancellationToken);

        if (provider is null)
        {
            return Error.NotFound("AiProvider.NotFound", "AI provider was not found.");
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
