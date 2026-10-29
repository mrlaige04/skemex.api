using Skemex.Application.Models.Projects;

namespace Skemex.Application.Services.Projects;

public interface IProjectTasksExportService
{
    ExportFileResult Generate(
        IReadOnlyList<ExportTaskRow> rows,
        ExportTasksFormat format,
        string projectCode);
}
