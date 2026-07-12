using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskCounterConfiguration : IEntityTypeConfiguration<ProjectTaskCounter>
{
    public void Configure(EntityTypeBuilder<ProjectTaskCounter> builder)
    {
        builder.ToTable("project_task_counters");
        builder.HasKey(counter => counter.Id);
        builder.HasIndex(counter => counter.ProjectId).IsUnique();

        builder
            .HasOne(counter => counter.Project)
            .WithOne(project => project.TaskCounter)
            .HasForeignKey<ProjectTaskCounter>(counter => counter.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
