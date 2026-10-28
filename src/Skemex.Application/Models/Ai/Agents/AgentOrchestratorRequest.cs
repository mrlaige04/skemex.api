using System.Text.Json;
using Skemex.Application.Models.Ai.Chat;
using Skemex.Application.Models.Ai.Completions;

namespace Skemex.Application.Models.Ai.Agents;

public sealed class AgentOrchestratorRequest
{
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public Guid? ProjectId { get; init; }
    public Guid? ChatId { get; init; }
    public Guid? AgentJobId { get; init; }

    /// <summary>When set, runs that tool via direct agent tool execution.</summary>
    public string? ToolName { get; init; }

    public required string UserInput { get; init; }
    public string? Model { get; init; }

    /// <summary>Optional JSON object of direct tool arguments (merged with userInput).</summary>
    public JsonElement? DirectArgs { get; init; }

    /// <summary>Optional raw JSON arguments string from the API/job payload.</summary>
    public string? ArgumentsJson { get; init; }

    public IReadOnlyList<AiChatAttachmentRef> AttachmentRefs { get; init; } = [];

    public IReadOnlyList<AiCompletionAttachment> CompletionAttachments { get; init; } = [];

    public IReadOnlyList<AiChatArtefactRef> ArtefactRefs { get; init; } = [];

    /// <summary>Markdown artefacts table (#|Type|Content) injected into tool user prompts.</summary>
    public string? ArtefactContext { get; init; }
}
