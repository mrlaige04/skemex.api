namespace Skemex.Domain.Entities.Projects;

/// <summary>A basic test scenario attached to a project task.</summary>
public sealed class ProjectTaskTestCase
{
    /// <summary><c>positive</c> or <c>negative</c>.</summary>
    public string Type { get; set; } = ProjectTaskTestCaseTypes.Positive;

    public string Description { get; set; } = string.Empty;

    public string ExpectedResult { get; set; } = string.Empty;
}

public static class ProjectTaskTestCaseTypes
{
    public const string Positive = "positive";
    public const string Negative = "negative";
}
