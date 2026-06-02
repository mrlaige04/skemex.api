using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class ContainsDigitValidator<T> : PropertyValidator<T, string>, IContainsDigitValidator
{
    public override string Name => "ContainsDigitValidator";

    public override bool IsValid(ValidationContext<T> context, string value)
        => !string.IsNullOrEmpty(value) && value.Any(char.IsDigit);

    protected override string GetDefaultMessageTemplate(string errorCode)
        => "'{PropertyName}' must contain at least one digit.";
}