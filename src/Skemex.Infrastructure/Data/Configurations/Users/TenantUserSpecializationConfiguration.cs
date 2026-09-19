using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantUserSpecializationConfiguration : IEntityTypeConfiguration<TenantUserSpecialization>
{
    public void Configure(EntityTypeBuilder<TenantUserSpecialization> builder)
    {
        builder.ToTable("tenants_users_specializations");
        builder.HasKey(entry => new { entry.TenantUserId, entry.TenantSpecializationId });

        builder
            .HasOne(entry => entry.TenantUser)
            .WithMany(user => user.Specializations)
            .HasForeignKey(entry => entry.TenantUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Specialization)
            .WithMany(specialization => specialization.Users)
            .HasForeignKey(entry => entry.TenantSpecializationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
