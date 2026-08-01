using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiTaskDecomposition;

public sealed class EnqueueAiTaskDecompositionCommand : ICommand<AiDecompositionJobDto>
{
    public Guid ProjectId { get; set; }
    public string UserInput { get; set; } = string.Empty;
    public string? CustomInstructions { get; set; }
}
