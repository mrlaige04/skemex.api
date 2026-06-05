using FluentValidation;

namespace Skemex.Infrastructure.Authentication.ForgotPassword;

public sealed class ResetPasswordWithCodeValidator : AbstractValidator<ResetPasswordWithCodeCommand>
{
    public ResetPasswordWithCodeValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().Length(6);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}
