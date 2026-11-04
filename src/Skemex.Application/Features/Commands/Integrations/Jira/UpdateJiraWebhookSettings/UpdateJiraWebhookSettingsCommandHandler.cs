using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira.Webhooks;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.UpdateJiraWebhookSettings;

public sealed class UpdateJiraWebhookSettingsCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<ProjectIntegration> integrationRepository,
    ITenantRepository<IntegrationWebhook> webhookRepository,
    IJiraWebhookService webhookService)
    : ICommandHandler<UpdateJiraWebhookSettingsCommand, JiraConnectionDto>
{
    public async Task<ErrorOr<JiraConnectionDto>> Handle(
        UpdateJiraWebhookSettingsCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before managing integrations.");
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

        if (integration is null || integration.Status != IntegrationStatus.Connected)
        {
            return Error.Validation(
                "Jira.NotConnected",
                "Connect and map a Jira project before configuring webhooks.");
        }

        // Local filter only — does not re-register with Jira.
        webhookService.ApplyEventSettings(integration, request.Events);
        var webhook = JiraWebhookEventSettingsHelper.PrimaryWebhook(integration);
        if (webhook is not null)
        {
            await webhookRepository.UpdateAsync(webhook, cancellationToken);
        }

        await integrationRepository.UpdateAsync(integration, cancellationToken);
        return JiraConnectionDtoHelper.Map(integration, webhookService);
    }
}
