namespace Skemex.Application.Features.TenantUsers.Queries.LookupUserByEmail;

public sealed class LookupUserByEmailResponse
{
    public bool Exists { get; init; }

    public bool AlreadyInWorkspace { get; init; }

    public bool CannotBeInvited { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}
