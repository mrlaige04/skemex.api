using Skemex.Application.Features.Abstractions;

namespace Skemex.Infrastructure.Authentication.SelectTenant;

public class SelectTenantCommand : ICommand<TenantSessionResponse>
{
    public Guid TenantId { get; set; }
}
