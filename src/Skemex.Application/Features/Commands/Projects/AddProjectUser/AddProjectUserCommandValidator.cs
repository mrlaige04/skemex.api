using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.AddProjectUser;

public sealed class AddProjectUserCommandValidator : AbstractValidator<AddProjectUserCommand>
{
    public AddProjectUserCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}
