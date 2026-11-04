using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.UpsertJiraEntityMapping;

public sealed class UpsertJiraEntityMappingCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraEntityMappingService mappingService)
    : ICommandHandler<UpsertJiraEntityMappingCommand, JiraMappingLinkDto>
{
    public async Task<ErrorOr<JiraMappingLinkDto>> Handle(
        UpsertJiraEntityMappingCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before updating mappings.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return await mappingService.UpsertAsync(
            request.ProjectId,
            new UpsertJiraEntityMappingRequest
            {
                LocalEntityType = request.LocalEntityType,
                LocalEntityId = request.LocalEntityId,
                ExternalEntityId = request.ExternalEntityId,
                ExternalEntityKey = request.ExternalEntityKey,
                Status = request.Status,
            },
            cancellationToken);
    }
}
