using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.EnqueueAiTaskDecomposition;

public sealed class EnqueueAiTaskDecompositionCommandValidator
    : AbstractValidator<EnqueueAiTaskDecompositionCommand>
{
    public EnqueueAiTaskDecompositionCommandValidator()
    {
        RuleFor(command => command.UserInput)
            .NotEmpty()
            .MaximumLength(8000);
    }
}
