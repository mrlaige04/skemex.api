using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommandValidator : AbstractValidator<UpdateProjectSettingsCommand>
{
    public UpdateProjectSettingsCommandValidator()
    {
        RuleFor(command => command)
            .Must(command =>
                command.DefaultTaskColumnId.HasValue
                || command.DefaultAiModelId.HasValue
                || command.ClearDefaultAiModel)
            .WithMessage("At least one settings field must be provided.");

        RuleFor(command => command.DefaultTaskColumnId)
            .NotEmpty()
            .When(command => command.DefaultTaskColumnId.HasValue);

        RuleFor(command => command.DefaultAiModelId)
            .NotEmpty()
            .When(command => command.DefaultAiModelId.HasValue && !command.ClearDefaultAiModel);
    }
}
