using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.DisconnectJira;

public sealed class DisconnectJiraCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<ProjectIntegration> integrationRepository,
    IJiraConnectionTokenService tokenService,
    IJiraWebhookService webhookService)
    : ICommandHandler<DisconnectJiraCommand>
{
    public async Task<ErrorOr<Success>> Handle(
        DisconnectJiraCommand request,
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

        if (integration is null)
        {
            return Result.Success;
        }

        string? accessToken = null;
        var tokenResult = await tokenService.GetValidAccessTokenAsync(integration, cancellationToken);
        if (!tokenResult.IsError)
        {
            accessToken = tokenResult.Value;
        }

        await webhookService.TryUnregisterAsync(integration, accessToken, cancellationToken);
        await integrationRepository.DeleteAsync(integration, cancellationToken);
        return Result.Success;
    }
}

