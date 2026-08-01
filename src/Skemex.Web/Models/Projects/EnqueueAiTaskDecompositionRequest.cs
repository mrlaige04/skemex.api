namespace Skemex.Web.Models.Projects;

public sealed class EnqueueAiTaskDecompositionRequest
{
    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
}
