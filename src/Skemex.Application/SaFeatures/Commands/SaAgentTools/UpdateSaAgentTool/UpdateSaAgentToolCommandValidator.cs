using FluentValidation;

namespace Skemex.Application.SaFeatures.Commands.SaAgentTools.UpdateSaAgentTool;

public sealed class UpdateSaAgentToolCommandValidator : AbstractValidator<UpdateSaAgentToolCommand>
{
    public const int MaxDescriptionLength = 20_000;
    public const int MaxSystemPromptLength = 100_000;

    public UpdateSaAgentToolCommandValidator()
    {
        RuleFor(x => x.ToolId).NotEmpty();
        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);
        RuleFor(x => x.SystemPrompt)
            .NotEmpty()
            .MaximumLength(MaxSystemPromptLength)
            .When(x => x.SystemPrompt is not null);
    }
}
