using FluentValidation;
using Skemex.Application.Models.Projects;

namespace Skemex.Application.Features.Queries.Projects.ExportProjectTasks;

public sealed class ExportProjectTasksQueryValidator : AbstractValidator<ExportProjectTasksQuery>
{
    public ExportProjectTasksQueryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Format).IsInEnum();
        RuleFor(x => x.Scope).IsInEnum();

        When(
            x => x.Scope == ExportTasksScope.Selected,
            () =>
            {
                RuleFor(x => x.SelectedTaskIds)
                    .NotEmpty()
                    .WithMessage("Select at least one issue to export.");
            });
    }
}
