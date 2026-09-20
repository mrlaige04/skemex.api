using Skemex.Domain.Entities.Abstractions;

namespace Skemex.Domain.Entities.Ai;

/// <summary>
/// SuperAdmin-editable agent tool configuration. Global (not tenant-scoped).
/// Matched to DI <c>IAgentTool</c> implementations by <see cref="SystemName"/>.
/// </summary>
public class AgentTool : BaseEntity
{
    /// <summary>Stable tool key (e.g. task_decomposition). Unique.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>LLM-facing description (function calling / tool picker).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Default system prompt used for direct tool execution (may include placeholders).</summary>
    public string SystemPrompt { get; set; } = string.Empty;
}
