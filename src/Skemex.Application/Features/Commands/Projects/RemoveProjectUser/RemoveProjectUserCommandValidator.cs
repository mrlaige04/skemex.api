using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.RemoveProjectUser;

public sealed class RemoveProjectUserCommandValidator : AbstractValidator<RemoveProjectUserCommand>
{
    public RemoveProjectUserCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}
