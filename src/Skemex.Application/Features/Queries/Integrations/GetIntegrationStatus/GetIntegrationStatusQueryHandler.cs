using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.GetIntegrationStatus;

public sealed class GetIntegrationStatusQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository)
    : IQueryHandler<GetIntegrationStatusQuery, IntegrationStatusDto>
{
    public async Task<ErrorOr<IntegrationStatusDto>> Handle(
        GetIntegrationStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing integrations.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (!TryParseProvider(request.Provider, out var provider, out var normalizedKey))
        {
            return Error.Validation(
                "Integration.UnknownProvider",
                $"Unknown integration provider '{request.Provider}'. Supported: jira.");
        }

        var connection = await connectionRepository.GetAsync(
            filter: c => c.ProjectId == request.ProjectId && c.Provider == provider,
            cancellationToken: cancellationToken);

        if (connection is null)
        {
            return new IntegrationStatusDto
            {
                Provider = normalizedKey,
                IsConfigured = false,
                HasConnection = false,
                Status = null,
            };
        }

        var isConfigured = IsConfigured(provider, connection);

        return new IntegrationStatusDto
        {
            Provider = normalizedKey,
            IsConfigured = isConfigured,
            HasConnection = true,
            Status = connection.Status.ToString(),
        };
    }

    private static bool IsConfigured(IntegrationProvider provider, IntegrationConnection connection) =>
        provider switch
        {
            IntegrationProvider.Jira =>
                connection.Status == IntegrationConnectionStatus.Connected
                && !string.IsNullOrWhiteSpace(connection.CloudId)
                && !string.IsNullOrWhiteSpace(connection.ExternalProjectKey),
            _ => connection.Status == IntegrationConnectionStatus.Connected,
        };

    private static bool TryParseProvider(
        string? raw,
        out IntegrationProvider provider,
        out string normalizedKey)
    {
        provider = default;
        normalizedKey = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var key = raw.Trim().ToLowerInvariant();
        if (key is "jira")
        {
            provider = IntegrationProvider.Jira;
            normalizedKey = "jira";
            return true;
        }

        return false;
    }
}
