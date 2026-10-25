namespace Skemex.Domain.Consts;

public static class RoleNames
{
    public const string Admin = nameof(Admin);
    public const string SuperAdmin = nameof(SuperAdmin);
    public const string Manager = nameof(Manager);
    public const string Developer = nameof(Developer);
    public const string QA = nameof(QA);

    /// <summary>Legacy tenant role renamed to <see cref="Developer"/> during RBAC seed.</summary>
    public const string LegacyUser = "User";

    public static IList<string> GetAll() => [Admin, SuperAdmin, Manager, Developer, QA];

    public static IReadOnlyList<string> GetTenantSystemRoles() => [Admin, Manager, Developer, QA];
}
