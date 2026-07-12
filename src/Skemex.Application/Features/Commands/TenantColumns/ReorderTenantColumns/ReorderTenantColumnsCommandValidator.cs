using FluentValidation;

namespace Skemex.Application.Features.Commands.TenantColumns.ReorderTenantColumns;

public sealed class ReorderTenantColumnsCommandValidator : AbstractValidator<ReorderTenantColumnsCommand>
{
    public ReorderTenantColumnsCommandValidator()
    {
        RuleFor(command => command.ColumnIds)
            .NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Column ids must be unique.");
    }
}
