using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.CreateSaAiProvider;

public sealed class CreateSaAiProviderCommand : ICommand<SaAiProviderDto>
{
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<SaAiProviderAuthEntryInput> AuthEntries { get; set; } = [];
}
