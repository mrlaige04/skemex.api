using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskWorkLogConfiguration : IEntityTypeConfiguration<ProjectTaskWorkLog>
{
    public void Configure(EntityTypeBuilder<ProjectTaskWorkLog> builder)
    {
        builder.ToTable("project_task_work_logs");
        builder.HasKey(entry => entry.Id);

        builder.HasIndex(entry => new { entry.TaskId, entry.StartedAt });
        builder.HasIndex(entry => new { entry.ProjectId, entry.UserId });

        builder.Property(entry => entry.StartedAt)
            .IsRequired();

        builder.Property(entry => entry.EndedAt)
            .IsRequired();

        builder.Property(entry => entry.SpentMinutes)
            .IsRequired();

        builder.Property(entry => entry.Comment)
            .HasMaxLength(2000);

        builder
            .HasOne(entry => entry.Project)
            .WithMany()
            .HasForeignKey(entry => entry.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Task)
            .WithMany(task => task.WorkLogs)
            .HasForeignKey(entry => entry.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.User)
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
