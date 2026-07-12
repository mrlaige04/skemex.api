using FluentValidation;

namespace Skemex.Application.Features.Commands.TenantColumns.UpdateTenantColumn;

public sealed class UpdateTenantColumnCommandValidator : AbstractValidator<UpdateTenantColumnCommand>
{
    public UpdateTenantColumnCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256)
            .When(command => command.Title is not null);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);

        RuleFor(command => command)
            .Must(command =>
                command.Title is not null ||
                command.Description is not null ||
                command.IsRequired is not null ||
                command.IsSortOrderForced is not null)
            .WithMessage("At least one field must be provided.");
    }
}
