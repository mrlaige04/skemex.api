using ErrorOr;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Application.Services.Integrations.Jira;

/// <summary>Resolves a valid (possibly refreshed) access token for a stored Jira connection.</summary>
public interface IJiraConnectionTokenService
{
    Task<ErrorOr<string>> GetValidAccessTokenAsync(
        ProjectIntegration connection,
        CancellationToken cancellationToken = default);
}
