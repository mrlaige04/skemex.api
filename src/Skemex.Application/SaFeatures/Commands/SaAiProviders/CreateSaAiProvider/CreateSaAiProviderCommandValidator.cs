using FluentValidation;
using Skemex.Application.Models.Ai;

namespace Skemex.Application.SaFeatures.Commands.SaAiProviders.CreateSaAiProvider;

public sealed class CreateSaAiProviderCommandValidator : AbstractValidator<CreateSaAiProviderCommand>
{
    public CreateSaAiProviderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("Key must contain only letters, digits, underscores, and hyphens.");
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
