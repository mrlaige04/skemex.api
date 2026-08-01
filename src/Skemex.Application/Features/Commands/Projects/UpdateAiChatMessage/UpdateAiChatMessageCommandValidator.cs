using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChatMessage;

public sealed class UpdateAiChatMessageCommandValidator : AbstractValidator<UpdateAiChatMessageCommand>
{
    public UpdateAiChatMessageCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ChatId).NotEmpty();
        RuleFor(command => command.MessageId).NotEmpty();
        RuleFor(command => command.Content).NotEmpty().MaximumLength(8000);
    }
}
