using FluentValidation;

namespace Skemex.Application.Features.Commands.Users.UpdateTenantUser;

public sealed class UpdateTenantUserCommandValidator : AbstractValidator<UpdateTenantUserCommand>
{
    public UpdateTenantUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => x.Email is not null);

        RuleFor(x => x.FirstName)
            .MaximumLength(128)
            .When(x => x.FirstName is not null);

        RuleFor(x => x.LastName)
            .MaximumLength(128)
            .When(x => x.LastName is not null);

        RuleFor(x => x.RoleName)
            .MaximumLength(128)
            .When(x => x.RoleName is not null);

        RuleFor(x => x.Skills)
            .Must(skills => skills is null || skills.Count <= 50)
            .WithMessage("A user cannot have more than 50 skills.");

        RuleForEach(x => x.Skills)
            .MaximumLength(100)
            .When(x => x.Skills is not null);

        RuleFor(x => x)
            .Must(HasAtLeastOneChange)
            .WithMessage("Provide at least one of: email, first name, last name, role, or skills.");
    }

    private static bool HasAtLeastOneChange(UpdateTenantUserCommand c) =>
        (c.Email is not null && c.Email.Trim().Length > 0) ||
        (c.FirstName is not null && c.FirstName.Trim().Length > 0) ||
        (c.LastName is not null && c.LastName.Trim().Length > 0) ||
        (c.RoleName is not null && c.RoleName.Trim().Length > 0) ||
        c.Skills is not null;
}
