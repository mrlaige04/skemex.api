using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskRelationConfiguration : IEntityTypeConfiguration<ProjectTaskRelation>
{
    public void Configure(EntityTypeBuilder<ProjectTaskRelation> builder)
    {
        builder.ToTable("project_task_relations");
        builder.HasKey(entry => entry.Id);

        builder
            .HasIndex(entry => new { entry.SourceTaskId, entry.TargetTaskId, entry.RelationType })
            .IsUnique();

        builder.HasIndex(entry => entry.TargetTaskId);
        builder.HasIndex(entry => new { entry.TenantId, entry.SourceTaskId });

        builder.Property(entry => entry.RelationType)
            .HasConversion<int>()
            .IsRequired();

        builder
            .HasOne(entry => entry.SourceTask)
            .WithMany(task => task.Relations)
            .HasForeignKey(entry => entry.SourceTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(entry => entry.TargetTask)
            .WithMany()
            .HasForeignKey(entry => entry.TargetTaskId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
