using FluentValidation;

namespace Skemex.Infrastructure.Authentication.SelectTenant;

public class SelectTenantValidator : AbstractValidator<SelectTenantCommand>
{
    public SelectTenantValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
    }
}
