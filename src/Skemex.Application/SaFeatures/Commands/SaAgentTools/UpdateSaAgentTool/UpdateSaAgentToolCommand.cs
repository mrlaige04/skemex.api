using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAgentTools;

namespace Skemex.Application.SaFeatures.Commands.SaAgentTools.UpdateSaAgentTool;

public sealed class UpdateSaAgentToolCommand : ICommand<SaAgentToolDto>
{
    public Guid ToolId { get; set; }

    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }
}
