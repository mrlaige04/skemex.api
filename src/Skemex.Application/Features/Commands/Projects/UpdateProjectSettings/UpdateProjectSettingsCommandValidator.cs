using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.UpdateProjectSettings;

public sealed class UpdateProjectSettingsCommandValidator : AbstractValidator<UpdateProjectSettingsCommand>
{
    public UpdateProjectSettingsCommandValidator()
    {
        RuleFor(command => command.DefaultTaskColumnId)
            .NotEmpty();
    }
}
