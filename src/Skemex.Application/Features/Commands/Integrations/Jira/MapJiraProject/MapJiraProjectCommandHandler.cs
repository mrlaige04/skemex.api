using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.MapJiraProject;

public sealed class MapJiraProjectCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<ProjectIntegration> integrationRepository,
    IJiraConnectionTokenService tokenService,
    IJiraWebhookService webhookService)
    : ICommandHandler<MapJiraProjectCommand, JiraConnectionDto>
{
    public async Task<ErrorOr<JiraConnectionDto>> Handle(
        MapJiraProjectCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before mapping Jira.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        if (string.IsNullOrWhiteSpace(request.CloudId)
            || string.IsNullOrWhiteSpace(request.ExternalProjectId)
            || string.IsNullOrWhiteSpace(request.ExternalProjectKey))
        {
            return Error.Validation(
                "Jira.MappingRequired",
                "Select a Jira site and project to complete the integration.");
        }

        var integration = await integrationRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            include: q => q.Include(i => i.Webhooks),
            cancellationToken: cancellationToken);

        if (integration is null
            || string.IsNullOrWhiteSpace(integration.EncryptedAccessToken))
        {
            return Error.NotFound(
                "Jira.ConnectionNotFound",
                "Connect Jira with OAuth before selecting a project.");
        }

        var projectChanged = !string.Equals(
            integration.ExternalProjectKey,
            request.ExternalProjectKey.Trim(),
            StringComparison.OrdinalIgnoreCase);

        integration.CloudId = request.CloudId.Trim();
        integration.SiteUrl = string.IsNullOrWhiteSpace(request.SiteUrl)
            ? integration.SiteUrl
            : request.SiteUrl.Trim();
        integration.SiteName = string.IsNullOrWhiteSpace(request.SiteName)
            ? integration.SiteName
            : request.SiteName.Trim();
        integration.ExternalProjectId = request.ExternalProjectId.Trim();
        integration.ExternalProjectKey = request.ExternalProjectKey.Trim();
        integration.ExternalProjectName = string.IsNullOrWhiteSpace(request.ExternalProjectName)
            ? request.ExternalProjectKey.Trim()
            : request.ExternalProjectName.Trim();
        integration.Status = IntegrationStatus.Connected;
        integration.LastError = null;

        // Remap → unregister old provider webhooks so EnsureRegistered creates a fresh one.
        if (projectChanged && integration.Webhooks.Any(w => !string.IsNullOrWhiteSpace(w.ExternalWebhookId)))
        {
            var oldToken = await tokenService.GetValidAccessTokenAsync(integration, cancellationToken);
            if (!oldToken.IsError)
            {
                await webhookService.TryUnregisterAsync(integration, oldToken.Value, cancellationToken);
            }
        }

        await integrationRepository.UpdateAsync(integration, cancellationToken);

        var tokenResult = await tokenService.GetValidAccessTokenAsync(integration, cancellationToken);
        if (!tokenResult.IsError)
        {
            await webhookService.EnsureRegisteredAsync(integration, tokenResult.Value, cancellationToken);
            integration = await integrationRepository.GetAsync(
                filter: c => c.Id == integration.Id,
                include: q => q.Include(i => i.Webhooks),
                cancellationToken: cancellationToken) ?? integration;
        }

        return JiraConnectionDtoHelper.Map(integration, webhookService);
    }
}
