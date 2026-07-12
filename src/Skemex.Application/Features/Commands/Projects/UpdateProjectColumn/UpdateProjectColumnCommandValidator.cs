using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectColumn;

public sealed class UpdateProjectColumnCommandValidator : AbstractValidator<UpdateProjectColumnCommand>
{
    public UpdateProjectColumnCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256)
            .When(command => command.Title is not null);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);

        RuleFor(command => command)
            .Must(command => command.Title is not null || command.Description is not null)
            .WithMessage("At least one field must be provided.");
    }
}
