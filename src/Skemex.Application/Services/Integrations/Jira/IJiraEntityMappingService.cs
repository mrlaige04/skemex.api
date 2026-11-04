using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Services.Integrations.Jira;

public interface IJiraEntityMappingService
{
    Task<ErrorOr<JiraEntityMappingWorkspaceDto>> GetWorkspaceAsync(
        Guid projectId,
        string localEntityType,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<JiraMappingLinkDto>> UpsertAsync(
        Guid projectId,
        UpsertJiraEntityMappingRequest request,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<Success>> DeleteAsync(
        Guid projectId,
        Guid mappingId,
        CancellationToken cancellationToken = default);
}
