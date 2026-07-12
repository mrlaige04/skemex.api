using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.CreateProjectColumn;

public sealed class CreateProjectColumnCommandValidator : AbstractValidator<CreateProjectColumnCommand>
{
    public CreateProjectColumnCommandValidator()
    {
        RuleFor(command => command)
            .Must(command => command.TenantColumnId is not null || !string.IsNullOrWhiteSpace(command.Key))
            .WithMessage("Provide a workspace column or a custom column key.");

        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Must(key => CreateProjectColumnCommand.NormalizeKey(key!).Length > 0)
            .WithMessage("Key must contain letters, numbers, or hyphens.")
            .When(command => command.TenantColumnId is null);

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256)
            .When(command => command.TenantColumnId is null);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);
    }
}
