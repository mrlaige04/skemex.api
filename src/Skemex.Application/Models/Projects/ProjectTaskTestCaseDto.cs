namespace Skemex.Application.Models.Projects;

public sealed class ProjectTaskTestCaseDto
{
    /// <summary><c>positive</c> or <c>negative</c>.</summary>
    public string Type { get; init; } = "positive";

    public string Description { get; init; } = string.Empty;

    public string ExpectedResult { get; init; } = string.Empty;
}
