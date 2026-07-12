using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.CreateSaTenant;

public sealed class CreateSaTenantCommandValidator : AbstractValidator<CreateSaTenantCommand>
{
    public CreateSaTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).MaximumLength(128);
        RuleFor(x => x.LastName).MaximumLength(128);
    }
}
