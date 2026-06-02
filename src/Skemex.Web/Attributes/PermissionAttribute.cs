using Microsoft.AspNetCore.Mvc;

namespace Skemex.Web.Attributes;

public class PermissionAttribute : TypeFilterAttribute
{
    public string[] Permissions { get; set; }
    public PermissionCombining Combining { get; set; }
    
    public PermissionAttribute(
        PermissionCombining combining = PermissionCombining.All, 
        params string[] permissions) 
        : base(typeof(PermissionFilter))
    {
        Permissions = permissions;
        Combining = combining;
        Arguments = [combining, permissions];
    }
}