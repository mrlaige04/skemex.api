using FluentValidation;

namespace Skemex.Application.Features.Commands.TenantSpecializations.UpdateTenantSpecialization;

public sealed class UpdateTenantSpecializationCommandValidator
    : AbstractValidator<UpdateTenantSpecializationCommand>
{
    public UpdateTenantSpecializationCommandValidator()
    {
        RuleFor(command => command.SpecializationId)
            .NotEmpty();

        RuleFor(command => command.Title)
            .MaximumLength(256)
            .When(command => command.Title is not null);

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
