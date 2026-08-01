using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.CreateAiChat;

public sealed class CreateAiChatCommandValidator : AbstractValidator<CreateAiChatCommand>
{
    public CreateAiChatCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Title)
            .MaximumLength(120)
            .When(command => !string.IsNullOrWhiteSpace(command.Title));
    }
}
