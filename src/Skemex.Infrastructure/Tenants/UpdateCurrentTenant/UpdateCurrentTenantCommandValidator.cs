using FluentValidation;

namespace Skemex.Infrastructure.Tenants.UpdateCurrentTenant;

public sealed class UpdateCurrentTenantCommandValidator : AbstractValidator<UpdateCurrentTenantCommand>
{
    public UpdateCurrentTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(256);
    }
}
