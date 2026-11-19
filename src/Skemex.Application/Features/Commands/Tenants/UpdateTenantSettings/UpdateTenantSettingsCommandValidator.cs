using FluentValidation;
using Skemex.Domain.Consts;

namespace Skemex.Application.Features.Commands.Tenants.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(command => command)
            .Must(command =>
                command.DefaultAiModelId.HasValue
                || command.ClearDefaultAiModel
                || command.EnrichmentAiModelId.HasValue
                || command.ClearEnrichmentAiModel
                || command.AiTemperature.HasValue)
            .WithMessage("At least one settings field must be provided.");

        RuleFor(command => command.DefaultAiModelId)
            .NotEmpty()
            .When(command => command.DefaultAiModelId.HasValue && !command.ClearDefaultAiModel);

        RuleFor(command => command.EnrichmentAiModelId)
            .NotEmpty()
            .When(command => command.EnrichmentAiModelId.HasValue && !command.ClearEnrichmentAiModel);

        RuleFor(command => command.AiTemperature)
            .InclusiveBetween(AiSamplingDefaults.MinTemperature, AiSamplingDefaults.MaxTemperature)
            .When(command => command.AiTemperature.HasValue);
    }
}
