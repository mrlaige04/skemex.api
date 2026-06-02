namespace Skemex.Infrastructure.Authentication;

public static class UserErrors
{
    public const string NotFound = "User.NotFound";
    public const string NotFoundDescription = "User was not found.";

    public const string EmailUnverified = "User.EmailUnverified";
    public const string EmailUnverifiedDescription = "Email is not confirmed.";

    public const string InvalidPassword = "User.InvalidPassword";
    public const string InvalidPasswordDescription = "Invalid password.";

    public const string TenantAccessDenied = "User.TenantAccessDenied";
    public const string TenantAccessDeniedDescription = "You do not have access to this tenant.";

    public const string EmailAlreadyExists = "User.EmailAlreadyExists";
    public const string EmailAlreadyExistsDescription = "An account with this email already exists.";

    public const string RegistrationFailed = "User.RegistrationFailed";

    public const string InvalidRefreshToken = "Auth.InvalidRefreshToken";
    public const string InvalidRefreshTokenDescription = "The refresh token is invalid or expired.";
}
