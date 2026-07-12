using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.SaTenants.UpdateSaTenant;

public sealed class UpdateSaTenantCommandValidator : AbstractValidator<UpdateSaTenantCommand>
{
    public UpdateSaTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(256).When(x => x.Name is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
