using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.CreateSaUser;

public sealed class CreateSaUserCommandValidator : AbstractValidator<CreateSaUserCommand>
{
    public CreateSaUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).MaximumLength(128);
    }
}
