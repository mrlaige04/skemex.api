using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class TenantColumnConfiguration : IEntityTypeConfiguration<TenantColumn>
{
    public void Configure(EntityTypeBuilder<TenantColumn> builder)
    {
        builder.ToTable("tenant_columns");
        builder.HasKey(column => column.Id);

        builder.HasIndex(column => new { column.TenantId, column.Key }).IsUnique();
        builder.HasIndex(column => new { column.TenantId, column.SortOrder }).IsUnique();

        builder.Property(column => column.Key)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(column => column.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(column => column.Description)
            .HasMaxLength(2000);

        builder
            .HasOne(column => column.Tenant)
            .WithMany(tenant => tenant.Columns)
            .HasForeignKey(column => column.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
