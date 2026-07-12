using FluentValidation;

namespace Skemex.Application.Features.Queries.Users.LookupUserByEmail;

public sealed class LookupUserByEmailQueryValidator : AbstractValidator<LookupUserByEmailQuery>
{
    public LookupUserByEmailQueryValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}
