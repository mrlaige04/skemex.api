using ErrorOr;
using Skemex.Application.Models.Integrations.Jira;

namespace Skemex.Application.Services.Integrations.Jira;

public interface IJiraTaskExportService
{
    Task<ErrorOr<ExportTasksToJiraResultDto>> ExportAsync(
        Guid projectId,
        ExportTasksToJiraRequest request,
        CancellationToken cancellationToken = default);
}
