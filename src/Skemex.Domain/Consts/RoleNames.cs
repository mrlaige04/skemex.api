namespace Skemex.Domain.Consts;

public class RoleNames
{
    public const string Admin = nameof(Admin);
    public const string SuperAdmin = nameof(SuperAdmin);
    public const string User = nameof(User);

    public static IList<string> GetAll() => [Admin, SuperAdmin, User];
}