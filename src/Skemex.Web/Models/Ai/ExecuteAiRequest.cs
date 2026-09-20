namespace Skemex.Web.Models.Ai;

public sealed class ExecuteAiRequest
{
    /// <summary>When set, runs that tool directly (e.g. task_decomposition). When null, chat + function calling.</summary>
    public string? ToolName { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? ChatId { get; set; }

    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
    public string? ArgumentsJson { get; set; }
    public string? Model { get; set; }
}
