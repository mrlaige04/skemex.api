using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.GetJiraConnection;

public sealed class GetJiraConnectionQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository)
    : IQueryHandler<GetJiraConnectionQuery, JiraConnectionResponseDto>
{
    public async Task<ErrorOr<JiraConnectionResponseDto>> Handle(
        GetJiraConnectionQuery request,
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

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        return new JiraConnectionResponseDto
        {
            Connection = connection is null ? null : JiraConnectionDtoHelper.Map(connection),
        };
    }
}
