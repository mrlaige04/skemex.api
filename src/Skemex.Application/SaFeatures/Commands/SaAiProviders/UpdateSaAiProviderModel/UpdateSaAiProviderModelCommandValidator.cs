using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProviderModel;

public sealed class UpdateSaAiProviderModelCommandValidator : AbstractValidator<UpdateSaAiProviderModelCommand>
{
    public UpdateSaAiProviderModelCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.ModelId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(160);
    }
}
