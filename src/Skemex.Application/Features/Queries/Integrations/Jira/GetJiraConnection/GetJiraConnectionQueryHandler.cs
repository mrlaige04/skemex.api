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

namespace Skemex.Application.Features.Queries.Integrations.Jira.GetJiraConnection;

public sealed class GetJiraConnectionQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<ProjectIntegration> integrationRepository,
    IJiraWebhookService webhookService)
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

        var integration = await integrationRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            include: q => q.Include(i => i.Webhooks),
            cancellationToken: cancellationToken);

        return new JiraConnectionResponseDto
        {
            Connection = integration is null
                ? null
                : JiraConnectionDtoHelper.Map(integration, webhookService),
        };
    }
}
