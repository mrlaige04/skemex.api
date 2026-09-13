namespace Skemex.Web.Models.Projects;

public sealed class CreateProjectTaskWorkLogRequest
{
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public string? Comment { get; set; }
}

public sealed class UpdateProjectTaskWorkLogRequest
{
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Comment { get; set; }
    public bool ClearComment { get; set; }
}
