using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderModels;

public sealed class GetSaAiProviderModelsQuery : IQuery<IReadOnlyList<SaAiProviderModelDto>>
{
    public Guid ProviderId { get; set; }
}
