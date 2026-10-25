using Microsoft.AspNetCore.Mvc;

namespace Skemex.Web.Attributes;

public class PermissionAttribute : TypeFilterAttribute
{
    public string[] Permissions { get; set; }
    public PermissionCombining Combining { get; set; }

    public PermissionAttribute(params string[] permissions)
        : base(typeof(PermissionFilter))
    {
        Permissions = permissions;
        Combining = PermissionCombining.All;
        Arguments = [PermissionCombining.All, permissions];
    }

    public PermissionAttribute(PermissionCombining combining, params string[] permissions)
        : base(typeof(PermissionFilter))
    {
        Permissions = permissions;
        Combining = combining;
        Arguments = [combining, permissions];
    }
}
