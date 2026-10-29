using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Services.Integrations.Jira;

public interface IJiraOAuthService
{
    bool IsConfigured { get; }

    Task<ErrorOr<JiraAuthorizeStartDto>> StartAuthorizationAsync(
        Guid projectId,
        Guid userId,
        Guid tenantId,
        string projectCode,
        CancellationToken cancellationToken = default);

    Task<Uri> HandleCallbackAsync(
        string? code,
        string? state,
        string? error,
        string? errorDescription,
        CancellationToken cancellationToken = default);
}
