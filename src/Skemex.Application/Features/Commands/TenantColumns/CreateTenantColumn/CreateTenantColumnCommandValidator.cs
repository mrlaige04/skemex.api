using FluentValidation;

namespace Skemex.Application.Features.Commands.TenantColumns.CreateTenantColumn;

public sealed class CreateTenantColumnCommandValidator : AbstractValidator<CreateTenantColumnCommand>
{
    public CreateTenantColumnCommandValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Must(key => CreateTenantColumnCommand.NormalizeKey(key).Length > 0)
            .WithMessage("Key must contain letters, numbers, or hyphens.");

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);
    }
}
