using FluentValidation;

namespace Skemex.Application.Features.Commands.Roles.CreateTenantRole;

public sealed class CreateTenantRoleCommandValidator : AbstractValidator<CreateTenantRoleCommand>
{
    public CreateTenantRoleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PermissionIds).NotNull();
    }
}
