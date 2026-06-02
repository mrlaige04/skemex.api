using FluentValidation;

namespace Skemex.Application.Features.Commands.Users.UpdateUserProfile;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(128)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(128)
            .When(x => x.LastName is not null);

        RuleFor(x => x)
            .Must(HasAtLeastOneChange)
            .WithMessage("Provide at least one of: first name, last name, or profile image.");
    }

    private static bool HasAtLeastOneChange(UpdateUserProfileCommand c) =>
        (c.FirstName is not null && c.FirstName.Trim().Length > 0) ||
        (c.LastName is not null && c.LastName.Trim().Length > 0) ||
        c.ProfileImage is not null;
}
