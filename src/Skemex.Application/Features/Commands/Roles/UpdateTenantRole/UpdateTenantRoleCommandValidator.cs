using FluentValidation;

namespace Skemex.Application.Features.Commands.Roles.UpdateTenantRole;

public sealed class UpdateTenantRoleCommandValidator : AbstractValidator<UpdateTenantRoleCommand>
{
    public UpdateTenantRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PermissionIds).NotNull();
    }
}
