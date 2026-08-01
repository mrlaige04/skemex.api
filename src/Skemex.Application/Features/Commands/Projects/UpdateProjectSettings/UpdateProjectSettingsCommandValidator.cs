using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommandValidator : AbstractValidator<UpdateProjectSettingsCommand>
{
    public UpdateProjectSettingsCommandValidator()
    {
        RuleFor(command => command)
            .Must(command =>
                command.DefaultTaskColumnId.HasValue
                || command.AiMaxTreeDepth.HasValue
                || command.AiMaxNodes.HasValue
                || command.DefaultAiModelId.HasValue
                || command.ClearDefaultAiModel)
            .WithMessage("At least one settings field must be provided.");

        RuleFor(command => command.DefaultTaskColumnId)
            .NotEmpty()
            .When(command => command.DefaultTaskColumnId.HasValue);

        RuleFor(command => command.AiMaxTreeDepth)
            .InclusiveBetween(1, 8)
            .When(command => command.AiMaxTreeDepth.HasValue);

        RuleFor(command => command.AiMaxNodes)
            .InclusiveBetween(1, 64)
            .When(command => command.AiMaxNodes.HasValue);

        RuleFor(command => command.DefaultAiModelId)
            .NotEmpty()
            .When(command => command.DefaultAiModelId.HasValue && !command.ClearDefaultAiModel);
    }
}
