using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiTaskDecomposition;

public sealed class EnqueueAiTaskDecompositionCommandValidator
    : AbstractValidator<EnqueueAiTaskDecompositionCommand>
{
    public EnqueueAiTaskDecompositionCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserInput)
            .NotEmpty()
            .MaximumLength(8000);
        RuleFor(command => command.CustomInstructions)
            .MaximumLength(4000)
            .When(command => command.CustomInstructions is not null);
    }
}
