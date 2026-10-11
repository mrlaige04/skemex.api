using Skemex.Domain.Entities.Projects;

namespace Skemex.Application.Services.Ai;

/// <summary>Shared prep data resolved by <c>BaseAgentTool</c> before tool-specific execution.</summary>
public sealed class ToolPreparedContext
{
    public required Guid ProjectId { get; init; }

    public required ProjectSettings Settings { get; init; }

    public required string ModelExternalId { get; init; }

    public required string ArgumentsJson { get; init; }
}
