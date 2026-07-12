using FluentValidation;
using FluentValidation.Validators;
using Skemex.Application.Validation.Abstractions;

namespace Skemex.Application.Validation;

public class ValidHttpUrlValidator<T> : PropertyValidator<T, string>, IValidHttpUrlValidator
{
    public override string Name => "ValidHttpUrlValidator";

    public override bool IsValid(ValidationContext<T> context, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    protected override string GetDefaultMessageTemplate(string errorCode) =>
        "'{PropertyName}' must be a valid HTTP or HTTPS URL.";
}