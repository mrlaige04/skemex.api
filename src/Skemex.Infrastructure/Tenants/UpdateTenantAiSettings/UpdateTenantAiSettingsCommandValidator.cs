using FluentValidation;
using Skemex.Domain.Consts;

namespace Skemex.Infrastructure.Tenants.UpdateTenantAiSettings;

public sealed class UpdateTenantAiSettingsCommandValidator : AbstractValidator<UpdateTenantAiSettingsCommand>
{
    public UpdateTenantAiSettingsCommandValidator()
    {
        RuleFor(command => command.AiTemperature)
            .InclusiveBetween(AiSamplingDefaults.MinTemperature, AiSamplingDefaults.MaxTemperature);
    }
}
