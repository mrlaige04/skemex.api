using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraSites;

public sealed class ListJiraSitesQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient)
    : IQueryHandler<ListJiraSitesQuery, IReadOnlyList<JiraAccessibleResourceDto>>
{
    public async Task<ErrorOr<IReadOnlyList<JiraAccessibleResourceDto>>> Handle(
        ListJiraSitesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing Jira sites.");
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

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        var sites = await jiraApiClient.GetAccessibleResourcesAsync(
            tokenResult.Value,
            cancellationToken);
        return sites.ToList();
    }
}
