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

        // One mapping per local entity per provider within a project.
        builder
            .HasIndex(x => new { x.TenantId, x.ProjectId, x.Provider, x.LocalEntityType, x.LocalEntityId })
            .IsUnique();

        // One external entity may map once per integration.
        builder
            .HasIndex(x => new { x.TenantId, x.ProjectIntegrationId, x.ExternalEntityType, x.ExternalEntityId })
            .IsUnique();

        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.LocalEntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalEntityId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExternalEntityKey).HasMaxLength(128);
        builder.Property(x => x.ExternalDataJson).HasColumnType("text");

        builder
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ProjectIntegration)
            .WithMany(i => i.Mappings)
            .HasForeignKey(x => x.ProjectIntegrationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Task)
            .WithMany(t => t.IntegrationMappings)
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TaskComment)
            .WithMany(c => c.IntegrationMappings)
            .HasForeignKey(x => x.TaskCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TaskWorkLog)
            .WithMany(w => w.IntegrationMappings)
            .HasForeignKey(x => x.TaskWorkLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TaskAttachment)
            .WithMany(a => a.IntegrationMappings)
            .HasForeignKey(x => x.TaskAttachmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.ProjectUser)
            .WithMany(pu => pu.IntegrationMappings)
            .HasForeignKey(x => x.ProjectUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TaskId);
        builder.HasIndex(x => x.TaskCommentId);
        builder.HasIndex(x => x.TaskWorkLogId);
        builder.HasIndex(x => x.TaskAttachmentId);
        builder.HasIndex(x => x.ProjectUserId);
        builder.HasIndex(x => x.ProjectIntegrationId);
    }
}
