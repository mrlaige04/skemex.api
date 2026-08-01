using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProvider;

public sealed class UpdateSaAiProviderCommand : ICommand<SaAiProviderDto>
{
    public Guid ProviderId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Null/blank keeps the existing API key.</summary>
    public string? ApiKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<SaAiProviderAuthEntryInput> AuthEntries { get; set; } = [];
}
