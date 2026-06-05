using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("tenants_users");
        builder.HasKey(tu => tu.Id);

        builder.HasIndex(tu => new { tu.UserId, tu.TenantId }).IsUnique();
        builder.HasIndex(tu => tu.InvitationToken).IsUnique();

        builder.Property(tu => tu.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(tu => tu.InvitationToken).HasMaxLength(128);

        builder
            .HasOne(tu => tu.User)
            .WithMany(u => u.Tenants)
            .HasForeignKey(tu => tu.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(tu => tu.Tenant)
            .WithMany(t => t.Users)
            .HasForeignKey(tu => tu.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}