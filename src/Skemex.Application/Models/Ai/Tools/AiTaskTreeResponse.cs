using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai.Tools;

public sealed class AiTaskTreeResponse
{
    [JsonPropertyName("root")]
    public AiTaskNode Root { get; set; } = null!;
}
