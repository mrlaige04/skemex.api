using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Entities.Integrations;
using Skemex.Domain.Enums;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.DisconnectJira;

public sealed class DisconnectJiraCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    ITenantRepository<IntegrationConnection> connectionRepository)
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

        var connection = await connectionRepository.GetAsync(
            filter: c =>
                c.ProjectId == request.ProjectId && c.Provider == IntegrationProvider.Jira,
            cancellationToken: cancellationToken);

        if (connection is null)
        {
            return Result.Success;
        }

        await connectionRepository.DeleteAsync(connection, cancellationToken);
        return Result.Success;
    }
}
