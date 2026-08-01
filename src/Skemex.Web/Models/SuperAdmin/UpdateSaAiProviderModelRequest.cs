using Skemex.Application.SaModels.SaAiProviders;

namespace Skemex.Web.Models.SuperAdmin;

public sealed class UpdateSaAiProviderModelRequest
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
