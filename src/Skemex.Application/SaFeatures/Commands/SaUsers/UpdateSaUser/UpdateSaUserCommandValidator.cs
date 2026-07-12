using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.SaUsers.UpdateSaUser;

public sealed class UpdateSaUserCommandValidator : AbstractValidator<UpdateSaUserCommand>
{
    public UpdateSaUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.FirstName).MaximumLength(128).When(x => x.FirstName is not null);
        RuleFor(x => x.LastName).MaximumLength(128).When(x => x.LastName is not null);
    }
}
