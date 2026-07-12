using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class OneOfValidator<T>(IEnumerable<string> allowedValues, bool ignoreCase = false)
    : PropertyValidator<T, string>, IOneOfValidator
{
    private readonly HashSet<string> _allowedValues = new(allowedValues,
        ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public override string Name => "OneOfValidator";

    public override bool IsValid(ValidationContext<T> context, string value)
        => !string.IsNullOrEmpty(value) && _allowedValues.Contains(value);
}