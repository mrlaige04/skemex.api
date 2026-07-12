namespace Skemex.Web.Models.Projects;

public sealed class CreateProjectForm
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IFormFile? Logo { get; set; }
}
