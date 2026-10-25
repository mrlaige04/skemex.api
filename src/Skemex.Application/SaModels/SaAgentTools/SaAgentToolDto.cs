namespace Skemex.Application.SaModels.SaAgentTools;

public sealed class SaAgentToolDto : SaAgentToolSummaryDto
{
    public string SystemPrompt { get; init; } = string.Empty;
}