using FluentValidation;
using Skemex.Domain.Consts;

namespace Skemex.Application.Features.Commands.Users.UpdateTenantUser;

public sealed class UpdateTenantUserCommandValidator : AbstractValidator<UpdateTenantUserCommand>
{
    private static readonly string[] AllowedRoles = [RoleNames.Admin, RoleNames.User];

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
            .Must(r => AllowedRoles.Contains(r!))
            .When(x => x.RoleName is not null)
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}.");

        RuleFor(x => x)
            .Must(HasAtLeastOneChange)
            .WithMessage("Provide at least one of: email, first name, last name, or role.");
    }

    private static bool HasAtLeastOneChange(UpdateTenantUserCommand c) =>
        (c.Email is not null && c.Email.Trim().Length > 0) ||
        (c.FirstName is not null && c.FirstName.Trim().Length > 0) ||
        (c.LastName is not null && c.LastName.Trim().Length > 0) ||
        (c.RoleName is not null && c.RoleName.Trim().Length > 0);
}
