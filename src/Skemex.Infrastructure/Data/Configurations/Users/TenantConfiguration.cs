using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => t.Email).IsUnique();

        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.Email).IsRequired();

        builder
            .HasMany(t => t.Users)
            .WithOne(tu => tu.Tenant)
            .HasForeignKey(tu => tu.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(t => t.Roles)
            .WithOne(r => r.Tenant)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}