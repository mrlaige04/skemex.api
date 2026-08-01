using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChat;

public sealed class UpdateAiChatCommandValidator : AbstractValidator<UpdateAiChatCommand>
{
    public UpdateAiChatCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ChatId).NotEmpty();
        RuleFor(command => command)
            .Must(command =>
                !string.IsNullOrWhiteSpace(command.Title)
                || command.AiModelId.HasValue
                || command.ClearAiModel)
            .WithMessage("Provide a title and/or AI model change.");
        RuleFor(command => command.Title)
            .MaximumLength(120)
            .When(command => !string.IsNullOrWhiteSpace(command.Title));
    }
}
