using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.TenantUsers.Queries.LookupUserByEmail;

public sealed class LookupUserByEmailQuery : IQuery<LookupUserByEmailResponse>
{
    public string Email { get; set; } = string.Empty;
}
