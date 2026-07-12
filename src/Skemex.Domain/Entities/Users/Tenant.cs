using Skemex.Domain.Entities.Abstractions;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Domain.Entities.Users;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;

    /// <summary>Blob identifier (storage object key) for the tenant logo; use <c>IUrlService</c> to build a download URL.</summary>
    public string? LogoBlobId { get; set; }

    public IList<TenantUser> Users { get; set; } = [];
    public IList<Project> Projects { get; set; } = [];
    public IList<TenantColumn> Columns { get; set; } = [];
    public IList<Role> Roles { get; set; } = [];
    public IList<PermissionGroup> PermissionGroups { get; set; } = [];
}