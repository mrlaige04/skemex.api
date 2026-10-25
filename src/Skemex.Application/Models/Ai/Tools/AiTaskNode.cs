using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai.Tools;

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
