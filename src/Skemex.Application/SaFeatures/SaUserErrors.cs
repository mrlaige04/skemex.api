namespace Skemex.Application.SaFeatures;

internal static class SaUserErrors
{
    public const string NotFound = "User.NotFound";
    public const string NotFoundDescription = "User was not found.";

    public const string SuperAdminProtected = "User.SuperAdminProtected";
    public const string SuperAdminProtectedDescription = "The platform super-admin account cannot be managed here.";

    public const string EmailAlreadyExists = "User.EmailAlreadyExists";
    public const string EmailAlreadyExistsDescription = "An account with this email already exists.";

    public const string CreateFailed = "User.CreateFailed";
    public const string UpdateFailed = "User.UpdateFailed";
    public const string DeleteFailed = "User.DeleteFailed";
}
