using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Tenants;

namespace Skemex.Infrastructure.Tenants.UpdateTenantAiSettings;

public sealed class UpdateTenantAiSettingsCommand : ICommand<TenantAiSettingsDto>
{
    public double AiTemperature { get; set; }
}
