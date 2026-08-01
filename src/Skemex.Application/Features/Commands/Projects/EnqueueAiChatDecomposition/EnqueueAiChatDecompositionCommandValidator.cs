using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiChatDecomposition;

public sealed class EnqueueAiChatDecompositionCommandValidator
    : AbstractValidator<EnqueueAiChatDecompositionCommand>
{
    public EnqueueAiChatDecompositionCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ChatId).NotEmpty();
        RuleFor(command => command.UserInput).NotEmpty().MaximumLength(8000);
        RuleFor(command => command.CustomInstructions)
            .MaximumLength(4000)
            .When(command => !string.IsNullOrWhiteSpace(command.CustomInstructions));
    }
}
