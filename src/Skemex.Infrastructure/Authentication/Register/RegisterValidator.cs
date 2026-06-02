using FluentValidation;

namespace Skemex.Infrastructure.Authentication.Register;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(c => c.Password)
            .NotEmpty();

        RuleFor(c => c.FirstName)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(c => c.LastName)
            .NotEmpty()
            .MaximumLength(128);
    }
}
