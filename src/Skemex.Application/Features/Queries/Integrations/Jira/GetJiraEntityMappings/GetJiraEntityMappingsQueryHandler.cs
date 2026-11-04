using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.GetJiraEntityMappings;

public sealed class GetJiraEntityMappingsQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraEntityMappingService mappingService)
    : IQueryHandler<GetJiraEntityMappingsQuery, JiraEntityMappingWorkspaceDto>
{
    public async Task<ErrorOr<JiraEntityMappingWorkspaceDto>> Handle(
        GetJiraEntityMappingsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before viewing mappings.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return await mappingService.GetWorkspaceAsync(
            request.ProjectId,
            request.LocalEntityType,
            cancellationToken);
    }
}
