using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiChatDecomposition;

public sealed class EnqueueAiChatDecompositionCommand : ICommand<AiDecompositionJobDto>
{
    public Guid ProjectId { get; init; }
    public Guid ChatId { get; init; }
    public required string UserInput { get; init; }
    public string? CustomInstructions { get; init; }
}
