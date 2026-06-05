using FluentValidation;

namespace Skemex.Infrastructure.Authentication.Invitations;

public sealed class AcceptTenantInvitationValidator : AbstractValidator<AcceptTenantInvitationCommand>
{
    public AcceptTenantInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password)
            .MinimumLength(6)
            .When(x => x.Password is not null);
    }
}
