using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Queries.SaAiProviders.GetSaAiProviders;

public sealed class GetSaAiProvidersQuery : IQuery<IReadOnlyList<SaAiProviderDto>>;
