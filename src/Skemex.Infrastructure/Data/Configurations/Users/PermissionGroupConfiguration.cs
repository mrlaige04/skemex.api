using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
{
    public void Configure(EntityTypeBuilder<PermissionGroup> builder)
    {
        builder.ToTable("permission_groups");
        builder.HasKey(pg => pg.Id);

        builder.Property(pg => pg.Name).IsRequired();
        builder.HasIndex(pg => new { pg.TenantId, pg.Name }).IsUnique();

        builder
            .HasMany(pg => pg.Permissions)
            .WithOne(p => p.Group)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}