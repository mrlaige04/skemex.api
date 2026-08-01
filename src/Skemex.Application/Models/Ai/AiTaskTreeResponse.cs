using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai;

public sealed class AiTaskTreeResponse
{
    [JsonPropertyName("root")]
    public AiTaskNode Root { get; set; } = null!;
}

public sealed class AiTaskNode
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("subtasks")]
    public List<AiTaskNode> Subtasks { get; set; } = [];
}
