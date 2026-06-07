using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.DeleteSaEmailTemplate;

public sealed class DeleteSaEmailTemplateCommandValidator : AbstractValidator<DeleteSaEmailTemplateCommand>
{
    public DeleteSaEmailTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
    }
}
