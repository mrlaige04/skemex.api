using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraUsers;

public sealed class ListJiraUsersQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository,
    IJiraConnectionTokenService tokenService,
    IJiraApiClient jiraApiClient)
    : IQueryHandler<ListJiraUsersQuery, IReadOnlyList<JiraUserDto>>
{
    public const int MaxUsers = 200;

    public async Task<ErrorOr<IReadOnlyList<JiraUserDto>>> Handle(
        ListJiraUsersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before listing Jira users.");
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

        if (connection is null
            || connection.Status != IntegrationConnectionStatus.Connected
            || string.IsNullOrWhiteSpace(connection.CloudId)
            || string.IsNullOrWhiteSpace(connection.ExternalProjectKey))
        {
            return Error.Validation(
                "Jira.NotConnected",
                "Connect Jira and map a project before listing users.");
        }

        var tokenResult = await tokenService.GetValidAccessTokenAsync(connection, cancellationToken);
        if (tokenResult.IsError)
        {
            return tokenResult.Errors;
        }

        try
        {
            var users = await jiraApiClient.GetAssignableUsersAsync(
                tokenResult.Value,
                connection.CloudId!,
                connection.ExternalProjectKey!,
                MaxUsers,
                cancellationToken);
            return users.ToList();
        }
        catch (Exception ex)
        {
            return Error.Failure("Jira.ListUsersFailed", Truncate(ex.Message, 400));
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
