using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.DeleteSaAiProvider;

public sealed class DeleteSaAiProviderCommand : ICommand
{
    public Guid ProviderId { get; set; }
}
