using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai;

public sealed class AiTaskTreeResponse
{
    [JsonPropertyName("root")]
    public AiTaskNode Root { get; set; } = null!;
}

public sealed class AiTaskNode
{
    /// <summary>Work item type: Feature, Task, or Bug. Empty until JSON provides a value (do not default to Task).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>Accept alias if the model still emits taskType instead of type.</summary>
    [JsonPropertyName("taskType")]
    public string? TaskTypeAlias
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Type))
            {
                Type = value.Trim();
            }
        }
    }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];

    [JsonPropertyName("risks")]
    public List<string> Risks { get; set; } = [];

    [JsonPropertyName("testCases")]
    public List<AiTaskTestCase> TestCases { get; set; } = [];

    [JsonPropertyName("estimatedHours")]
    public decimal? EstimatedHours { get; set; }

    [JsonPropertyName("remainingHours")]
    public decimal? RemainingHours { get; set; }

    [JsonPropertyName("storyPoints")]
    public decimal? StoryPoints { get; set; }

    /// <summary>User id of suggested assignee; null for Features or when no match.</summary>
    [JsonPropertyName("assigneeId")]
    public Guid? AssigneeId { get; set; }

    [JsonPropertyName("subtasks")]
    public List<AiTaskNode> Subtasks { get; set; } = [];
}

public sealed class AiTaskTestCase
{
    /// <summary><c>positive</c> or <c>negative</c>. JSON field is <c>caseType</c>.</summary>
    [JsonIgnore]
    public string Type { get; set; } = "positive";

    [JsonPropertyName("caseType")]
    public string? CaseTypeJson
    {
        get => Type;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                Type = value.Trim().ToLowerInvariant();
            }
        }
    }

    /// <summary>Accept legacy "type" on test cases for compatibility.</summary>
    [JsonPropertyName("type")]
    public string? TypeJsonFallback
    {
        get => null;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim().ToLowerInvariant();
            if (trimmed is "positive" or "negative")
            {
                Type = trimmed;
            }
        }
    }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("expectedResult")]
    public string ExpectedResult { get; set; } = string.Empty;
}
