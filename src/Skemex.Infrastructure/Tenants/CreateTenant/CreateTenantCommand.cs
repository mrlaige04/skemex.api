using Skemex.Application.Features.Abstractions;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Tenants.CreateTenant;

public class CreateTenantCommand : ICommand<TenantSummaryResponse>
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
}
