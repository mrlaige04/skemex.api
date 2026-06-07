namespace Skemex.Application.Features.TenantUsers;

internal static class TenantUserErrors
{
    public const string EmailAlreadyExists = "User.EmailAlreadyExists";
    public const string EmailAlreadyExistsDescription = "An account with this email already exists.";
    public const string RegistrationFailed = "User.RegistrationFailed";

    public const string SuperAdminEmailReserved = "User.SuperAdminEmailReserved";
    public const string SuperAdminEmailReservedDescription = "This email address is reserved and cannot be used.";
}
