using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.DeleteJiraEntityMapping;

public sealed class DeleteJiraEntityMappingCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraEntityMappingService mappingService)
    : ICommandHandler<DeleteJiraEntityMappingCommand, Success>
{
    public async Task<ErrorOr<Success>> Handle(
        DeleteJiraEntityMappingCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before deleting mappings.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return await mappingService.DeleteAsync(
            request.ProjectId,
            request.MappingId,
            cancellationToken);
    }
}