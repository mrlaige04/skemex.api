using FluentValidation;

namespace Skemex.Infrastructure.Tenants.CreateTenant;

public class CreateTenantValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}
