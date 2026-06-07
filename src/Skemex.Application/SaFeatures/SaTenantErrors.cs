namespace Skemex.Application.SaFeatures;

internal static class SaTenantErrors
{
    public const string NotFound = "Tenant.NotFound";
    public const string NotFoundDescription = "Workspace was not found.";

    public const string NameTaken = "Tenant.NameTaken";
    public const string NameTakenDescription = "A company with this name already exists.";

    public const string EmailTaken = "Tenant.EmailTaken";
    public const string EmailTakenDescription = "A company with this email already exists.";

    public const string RoleCreateFailed = "Tenant.RoleCreateFailed";

    public const string UserCreateFailed = "Tenant.UserCreateFailed";
}
