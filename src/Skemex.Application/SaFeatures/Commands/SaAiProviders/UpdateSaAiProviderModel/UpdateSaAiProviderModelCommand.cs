using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProviderModel;

public sealed class UpdateSaAiProviderModelCommand : ICommand<SaAiProviderModelDto>
{
    public Guid ProviderId { get; set; }

    public Guid ModelId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
