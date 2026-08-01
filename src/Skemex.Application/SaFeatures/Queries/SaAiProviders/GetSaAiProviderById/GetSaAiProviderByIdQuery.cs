using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviderById;

public sealed class GetSaAiProviderByIdQuery : IQuery<SaAiProviderDto>
{
    public Guid ProviderId { get; set; }
}
