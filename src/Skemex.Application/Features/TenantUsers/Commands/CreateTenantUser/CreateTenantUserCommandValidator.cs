using FluentValidation;
using Skemex.Domain.Consts;

namespace Skemex.Application.Features.TenantUsers.Commands.CreateTenantUser;

public sealed class CreateTenantUserCommandValidator : AbstractValidator<CreateTenantUserCommand>
{
    private static readonly string[] AllowedRoles = [RoleNames.Admin, RoleNames.User];

    public CreateTenantUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.RoleName).NotEmpty().Must(r => AllowedRoles.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}.");
    }
}
