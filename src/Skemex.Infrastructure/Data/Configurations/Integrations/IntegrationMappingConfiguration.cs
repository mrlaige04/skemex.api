using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Infrastructure.Data.Configurations.Integrations;

public sealed class IntegrationMappingConfiguration : IEntityTypeConfiguration<IntegrationMapping>
{
    public void Configure(EntityTypeBuilder<IntegrationMapping> builder)
    {
        builder.ToTable("integration_mappings");
        builder.HasKey(x => x.Id);

        builder
            .HasIndex(x => new { x.TenantId, x.IntegrationConnectionId, x.LocalEntityType, x.LocalEntityId })
            .IsUnique();

        builder
            .HasIndex(x => new { x.TenantId, x.IntegrationConnectionId, x.ExternalEntityType, x.ExternalEntityId })
            .IsUnique();

        builder.Property(x => x.LocalEntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEntityId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExternalEntityKey).HasMaxLength(128);

        builder
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Connection)
            .WithMany(c => c.Mappings)
            .HasForeignKey(x => x.IntegrationConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
