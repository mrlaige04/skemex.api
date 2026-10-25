using System.Text.Json.Serialization;

namespace Skemex.Application.Models.Ai.Tools;

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
