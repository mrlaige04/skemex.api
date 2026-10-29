using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.MapJiraProject;

public sealed class MapJiraProjectCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository)
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

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (connection is null
            || string.IsNullOrWhiteSpace(connection.EncryptedAccessToken))
        {
            return Error.NotFound(
                "Jira.ConnectionNotFound",
                "Connect Jira with OAuth before selecting a project.");
        }

        connection.CloudId = request.CloudId.Trim();
        connection.SiteUrl = string.IsNullOrWhiteSpace(request.SiteUrl)
            ? connection.SiteUrl
            : request.SiteUrl.Trim();
        connection.SiteName = string.IsNullOrWhiteSpace(request.SiteName)
            ? connection.SiteName
            : request.SiteName.Trim();
        connection.ExternalProjectId = request.ExternalProjectId.Trim();
        connection.ExternalProjectKey = request.ExternalProjectKey.Trim();
        connection.ExternalProjectName = string.IsNullOrWhiteSpace(request.ExternalProjectName)
            ? request.ExternalProjectKey.Trim()
            : request.ExternalProjectName.Trim();
        connection.Status = IntegrationConnectionStatus.Connected;
        connection.LastError = null;

        await connectionRepository.UpdateAsync(connection, cancellationToken);
        return JiraConnectionDtoHelper.Map(connection);
    }
}
