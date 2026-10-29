using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Projects;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.StartJiraOAuth;

public sealed class StartJiraOAuthCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<Project> projectRepository,
    IJiraOAuthService jiraOAuthService)
    : ICommandHandler<StartJiraOAuthCommand, JiraAuthorizeStartDto>
{
    public async Task<ErrorOr<JiraAuthorizeStartDto>> Handle(
        StartJiraOAuthCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.GetTenantId();
        var userId = currentUser.GetUserId();
        if (tenantId is null || userId is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before connecting Jira.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        var project = await projectRepository.GetAsync(
            filter: p => p.Id == request.ProjectId,
            cancellationToken: cancellationToken);
        if (project is null)
        {
            return Error.NotFound("Project.NotFound", "Project was not found.");
        }

        return await jiraOAuthService.StartAuthorizationAsync(
            request.ProjectId,
            userId.Value,
            tenantId.Value,
            project.Code,
            cancellationToken);
    }
}
