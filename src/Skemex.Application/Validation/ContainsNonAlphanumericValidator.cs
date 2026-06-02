using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class ContainsNonAlphanumericValidator<T> : PropertyValidator<T, string>, IContainsNonAlphanumericValidator {
    public override string Name => "ContainsNonAlphanumericValidator";

    public override bool IsValid(ValidationContext<T> context, string value) =>
        !string.IsNullOrEmpty(value) && value.Any(ch => !char.IsLetterOrDigit(ch));

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must contain at least one special character.";
}