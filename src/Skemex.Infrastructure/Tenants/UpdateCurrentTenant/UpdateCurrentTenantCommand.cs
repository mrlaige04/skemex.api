using Skemex.Application.Features.Abstractions;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.UpdateCurrentTenant;

public sealed class UpdateCurrentTenantCommand : ICommand<TenantSummaryResponse>
{
    public string Name { get; set; } = string.Empty;
}
