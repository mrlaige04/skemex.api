using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Users;

namespace Skemex.Application.Features.Queries.Users.LookupUserByEmail;

public sealed class LookupUserByEmailQuery : IQuery<LookupUserByEmailResponse>
{
    public string Email { get; set; } = string.Empty;
}
