using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Integrations.Jira;
using Skemex.Application.Services.Integrations.Jira;
using Skemex.Application.Services.Projects;
using Skemex.Domain.Services;

namespace Skemex.Application.Features.Commands.Integrations.Jira.ImportTasksFromJira;

public sealed class ImportTasksFromJiraCommandHandler(
    ICurrentUser currentUser,
    IProjectAccessService projectAccessService,
    IJiraTaskImportService importService)
    : ICommandHandler<ImportTasksFromJiraCommand, ImportTasksFromJiraResultDto>
{
    public async Task<ErrorOr<ImportTasksFromJiraResultDto>> Handle(
        ImportTasksFromJiraCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.GetTenantId() is null)
        {
            return Error.Forbidden("Tenant.Required", "Select a workspace before importing from Jira.");
        }

        var access = await projectAccessService.EnsureCanAccessProjectAsync(
            request.ProjectId,
            cancellationToken);
        if (access.IsError)
        {
            return access.Errors;
        }

        return await importService.ImportAsync(
            request.ProjectId,
            new ImportTasksFromJiraRequest
            {
                ImportAll = request.ImportAll,
                IssueKeys = request.IssueKeys.ToList(),
                SkipAlreadyImported = request.SkipAlreadyImported,
            },
            cancellationToken);
    }
}
