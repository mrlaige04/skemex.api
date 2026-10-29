using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraProjects;

public sealed class ListJiraProjectsQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient)
    : IQueryHandler<ListJiraProjectsQuery, IReadOnlyList<JiraProjectDto>>
{
    public async Task<ErrorOr<IReadOnlyList<JiraProjectDto>>> Handle(
        ListJiraProjectsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing Jira projects.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (connection is null)
        {
            return Error.NotFound("Jira.ConnectionNotFound", "Jira is not connected for this project.");
        }

        var cloudId = string.IsNullOrWhiteSpace(request.CloudId)
            ? connection.CloudId
            : request.CloudId.Trim();

        if (string.IsNullOrWhiteSpace(cloudId))
        {
            return Error.Validation("Jira.CloudIdRequired", "Select a Jira site before listing projects.");
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        var projects = await jiraApiClient.GetProjectsAsync(
            tokenResult.Value,
            cloudId,
            cancellationToken);
        return projects.ToList();
    }
}
