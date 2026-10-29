using System.Text.Json.Serialization;
using Skemex.Application.Models.Projects;

namespace Skemex.Web.Models.Projects;

public sealed class ExportTasksRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExportTasksScope Scope { get; set; } = ExportTasksScope.AllFiltered;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ExportTasksFormat Format { get; set; } = ExportTasksFormat.Xlsx;

    public List<Guid>? SelectedTaskIds { get; set; }

    public string? Search { get; set; }

    public Guid? ColumnId { get; set; }

    public Guid? AssigneeId { get; set; }

    public bool Unassigned { get; set; }

    public string Sort { get; set; } = "createdAtDesc";
}
