using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateAiChat;

public sealed class UpdateAiChatCommandValidator : AbstractValidator<UpdateAiChatCommand>
{
    public UpdateAiChatCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ChatId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(120);
    }
}
