using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateProject;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(256);
        RuleFor(command => command.Description).MaximumLength(2000);
    }
}
