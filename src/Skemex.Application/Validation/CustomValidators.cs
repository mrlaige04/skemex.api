using FluentValidation;

namespace Skemex.Application.Validation;

public static class CustomValidators
{
    extension<T>(IRuleBuilder<T, string> ruleBuilder)
    {
        public IRuleBuilderOptions<T, string> ContainsDigit()
            => ruleBuilder.SetValidator(new ContainsDigitValidator<T>());

        public IRuleBuilderOptions<T, string> ContainsUppercase()
            => ruleBuilder.SetValidator(new ContainsUppercaseValidator<T>());

        public IRuleBuilderOptions<T, string> ContainsLowercase()
            => ruleBuilder.SetValidator(new ContainsLowercaseValidator<T>());

        public IRuleBuilderOptions<T, string> ContainsNonAlphanumeric()
            => ruleBuilder.SetValidator(new ContainsNonAlphanumericValidator<T>());

        public IRuleBuilderOptions<T, string> IsOneOf(IEnumerable<string> values, bool ignoreCase = false)
            => ruleBuilder.SetValidator(new OneOfValidator<T>(values, ignoreCase));

        public IRuleBuilderOptions<T, string> IsValidUrl()
            => ruleBuilder.SetValidator(new ValidHttpUrlValidator<T>());
    }
}