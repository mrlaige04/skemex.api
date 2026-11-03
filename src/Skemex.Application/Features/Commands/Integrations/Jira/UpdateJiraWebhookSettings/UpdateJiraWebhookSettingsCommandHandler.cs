using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Helpers.Integrations;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.UpdateJiraWebhookSettings;

public sealed class UpdateJiraWebhookSettingsCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository,
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

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (connection is null || connection.Status != IntegrationConnectionStatus.Connected)
        {
            return Error.Validation(
                "Jira.NotConnected",
                "Connect and map a Jira project before configuring webhooks.");
        }

        // Local filter only — Jira registration already includes all catalog events.
        webhookService.ApplyEventSettings(connection, request.Events);
        await connectionRepository.UpdateAsync(connection, cancellationToken);
        return JiraConnectionDtoHelper.Map(connection, webhookService);
    }
}
