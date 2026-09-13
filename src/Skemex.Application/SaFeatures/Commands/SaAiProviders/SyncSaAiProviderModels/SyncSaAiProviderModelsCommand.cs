using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.SyncSaAiProviderModels;

public sealed class SyncSaAiProviderModelsCommand : ICommand<IReadOnlyList<SaAiProviderModelDto>>
{
    public Guid ProviderId { get; set; }
}
