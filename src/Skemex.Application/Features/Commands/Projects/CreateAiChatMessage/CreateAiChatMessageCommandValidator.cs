using FluentValidation;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChatMessage;

public sealed class CreateAiChatMessageCommandValidator : AbstractValidator<CreateAiChatMessageCommand>
{
    public CreateAiChatMessageCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.ChatId).NotEmpty();
        RuleFor(command => command.Content).NotEmpty().MaximumLength(8000);
        RuleFor(command => command.Role).IsInEnum();
    }
}
