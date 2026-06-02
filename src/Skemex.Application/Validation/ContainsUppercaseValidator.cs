using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class ContainsUppercaseValidator<T> : PropertyValidator<T, string>, IContainsUppercaseValidator {
    public override string Name => "ContainsUppercaseValidator";

    public override bool IsValid(ValidationContext<T> context, string value) =>
        !string.IsNullOrEmpty(value) && value.Any(char.IsUpper);

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must contain at least one uppercase letter.";
}