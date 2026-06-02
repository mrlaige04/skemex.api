using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class ContainsLowercaseValidator<T> : PropertyValidator<T, string>, IContainsLowercaseValidator {
    public override string Name => "ContainsLowercaseValidator";

    public override bool IsValid(ValidationContext<T> context, string value) =>
        !string.IsNullOrEmpty(value) && value.Any(char.IsLower);

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must contain at least one lowercase letter.";
}