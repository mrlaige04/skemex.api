using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Web.Models.SuperAdmin;

public sealed class UpdateSaAiProviderRequest
{
    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<SaAiProviderAuthEntryInput>? AuthEntries { get; set; }
}
