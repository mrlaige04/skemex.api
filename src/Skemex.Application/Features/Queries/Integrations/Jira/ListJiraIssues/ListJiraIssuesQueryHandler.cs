using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Queries.Integrations.Jira.ListJiraIssues;

public sealed class ListJiraIssuesQueryHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraTaskImportService importService)
    : IQueryHandler<ListJiraIssuesQuery, IReadOnlyList<JiraIssuePreviewDto>>
{
    public async Task<ErrorOr<IReadOnlyList<JiraIssuePreviewDto>>> Handle(
        ListJiraIssuesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before listing Jira issues.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return await importService.ListRemoteIssuesAsync(request.ProjectId, cancellationToken);
    }
}
