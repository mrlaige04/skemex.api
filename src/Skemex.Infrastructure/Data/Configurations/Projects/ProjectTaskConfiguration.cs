using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("project_tasks");
        builder.HasKey(task => task.Id);

        builder.HasIndex(task => new { task.ProjectColumnId, task.ParentId });
        builder.HasIndex(task => new { task.ProjectId, task.ProjectColumnId });
        builder.HasIndex(task => new { task.ProjectId, task.Code }).IsUnique();

        builder.Property(task => task.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(task => task.Code)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(2000);

        builder
            .HasOne(task => task.Project)
            .WithMany(project => project.Tasks)
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(task => task.Column)
            .WithMany(column => column.Tasks)
            .HasForeignKey(task => task.ProjectColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(task => task.Assignee)
            .WithMany()
            .HasForeignKey(task => task.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(task => task.Reporter)
            .WithMany()
            .HasForeignKey(task => task.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(task => task.Parent)
            .WithMany(task => task.Subtasks)
            .HasForeignKey(task => task.ParentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
