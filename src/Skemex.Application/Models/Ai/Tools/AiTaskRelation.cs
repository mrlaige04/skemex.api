using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai.Tools;

/// <summary>
/// Directed link declared on a generated task node.
/// The owning node is the relation source; <see cref="SourceTaskId"/> names the related task (target),
/// matching the decomposition prompt examples (<c>BlockedBy</c> = owning node is blocked by that task).
/// </summary>
public sealed class AiTaskRelation
{
    /// <summary>
    /// Related task id (existing backlog Guid from ### RECENT_TASKS, or a Guid the model assigned
    /// to another node in this tree via <c>id</c>).
    /// Named sourceTaskId in the model JSON for prompt compatibility.
    /// </summary>
    [JsonPropertyName("sourceTaskId")]
    public string SourceTaskId { get; set; } = string.Empty;

    /// <summary>Alias accepted if the model emits targetTaskId / relatedTaskId.</summary>
    [JsonPropertyName("targetTaskId")]
    public string? TargetTaskIdAlias
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SourceTaskId))
            {
                SourceTaskId = value.Trim();
            }
        }
    }

    [JsonPropertyName("relatedTaskId")]
    public string? RelatedTaskIdAlias
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(SourceTaskId))
            {
                SourceTaskId = value.Trim();
            }
        }
    }

    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = string.Empty;
}
