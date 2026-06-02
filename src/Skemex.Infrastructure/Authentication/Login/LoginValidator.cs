using FluentValidation;

namespace Skemex.Infrastructure.Authentication.Login;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .NotNull()
            .EmailAddress();

        RuleFor(c => c.Password)
            .NotEmpty()
            .NotNull();
    }
}