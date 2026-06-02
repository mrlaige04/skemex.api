using FluentValidation;

namespace Skemex.Infrastructure.Authentication.RefreshToken;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(c => c.AccessToken).NotEmpty();
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}
