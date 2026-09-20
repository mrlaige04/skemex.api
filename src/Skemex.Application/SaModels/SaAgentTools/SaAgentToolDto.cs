namespace Skemex.Application.SaModels.SaAgentTools;

public class SaAgentToolSummaryDto
{
    public Guid Id { get; init; }

    public string SystemName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed class SaAgentToolDto : SaAgentToolSummaryDto
{
    public string SystemPrompt { get; init; } = string.Empty;
}
