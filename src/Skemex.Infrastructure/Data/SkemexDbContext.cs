using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data;

public class SkemexDbContext(DbContextOptions<SkemexDbContext> options) : IdentityDbContext<User, Role, Guid>(options)
{
    public DbSet<Tenant> Tenants { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        builder.Ignore<IdentityUserRole<Guid>>();

        var role = builder.Entity<Role>();
        foreach (var index in role.Metadata.GetIndexes().ToList())
        {
            if (index.Properties.Count == 1
                && index.Properties[0].Name == nameof(IdentityRole<Guid>.NormalizedName)
                && index.IsUnique)
            {
                role.Metadata.RemoveIndex(index);
            }
        }

        role.HasIndex(r => r.NormalizedName)
            .IsUnique()
            .HasFilter("\"TenantId\" IS NULL");

        role.HasIndex(r => new { r.TenantId, r.NormalizedName })
            .IsUnique()
            .HasFilter("\"TenantId\" IS NOT NULL");

        var userRole = builder.Entity<UserRole>();
        userRole.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasFilter("\"TenantId\" IS NULL");

        userRole.HasIndex(ur => new { ur.UserId, ur.RoleId, ur.TenantId })
            .IsUnique()
            .HasFilter("\"TenantId\" IS NOT NULL");
    }
}