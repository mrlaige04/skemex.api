using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectTask;

public sealed class CreateProjectTaskCommandValidator : AbstractValidator<CreateProjectTaskCommand>
{
    public CreateProjectTaskCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);
    }
}
