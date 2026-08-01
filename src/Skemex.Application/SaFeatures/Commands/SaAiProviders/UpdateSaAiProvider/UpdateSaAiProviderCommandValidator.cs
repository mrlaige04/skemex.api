using FluentValidation;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.UpdateSaAiProvider;

public sealed class UpdateSaAiProviderCommandValidator : AbstractValidator<UpdateSaAiProviderCommand>
{
    public UpdateSaAiProviderCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(512);
        RuleForEach(x => x.AuthEntries).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Name).NotEmpty().MaximumLength(128);
            entry.RuleFor(e => e.Type)
                .Must(type =>
                    string.Equals(type, AiProviderAuthEntryTypes.Header, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type, AiProviderAuthEntryTypes.Query, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Auth entry type must be 'header' or 'query'.");
        });
    }
}
