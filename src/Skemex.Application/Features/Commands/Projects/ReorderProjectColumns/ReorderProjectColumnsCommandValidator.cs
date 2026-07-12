using FluentValidation;

namespace Skemex.Application.Features.Commands.Projects.ReorderProjectColumns;

public sealed class ReorderProjectColumnsCommandValidator : AbstractValidator<ReorderProjectColumnsCommand>
{
    public ReorderProjectColumnsCommandValidator()
    {
        RuleFor(command => command.ColumnIds)
            .NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Column ids must be unique.");
    }
}
