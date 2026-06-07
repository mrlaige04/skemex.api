using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.UpdateSaEmailTemplate;

public sealed class UpdateSaEmailTemplateCommandValidator : AbstractValidator<UpdateSaEmailTemplateCommand>
{
    public UpdateSaEmailTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256).When(x => x.Title is not null);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(512).When(x => x.Subject is not null);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(100_000).When(x => x.Body is not null);
    }
}
