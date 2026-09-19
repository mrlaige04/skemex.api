using FluentValidation;

namespace Skemex.Application.Features.Commands.TenantSpecializations.CreateTenantSpecialization;

public sealed class CreateTenantSpecializationCommandValidator
    : AbstractValidator<CreateTenantSpecializationCommand>
{
    public CreateTenantSpecializationCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Description)
            .MaximumLength(2000)
            .When(command => command.Description is not null);

        RuleFor(command => command.DefaultSkills)
            .Must(skills => skills is null || skills.Count <= 50)
            .WithMessage("A specialization cannot have more than 50 default skills.");

        RuleForEach(command => command.DefaultSkills)
            .MaximumLength(100)
            .When(command => command.DefaultSkills is not null);
    }
}
