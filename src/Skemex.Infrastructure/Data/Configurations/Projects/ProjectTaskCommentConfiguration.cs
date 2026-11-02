using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskCommentConfiguration : IEntityTypeConfiguration<ProjectTaskComment>
{
    public void Configure(EntityTypeBuilder<ProjectTaskComment> builder)
    {
        builder.ToTable("project_task_comments");
        builder.HasKey(entry => entry.Id);

        builder.HasIndex(entry => new { entry.TaskId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.ProjectId, entry.AuthorId });

        builder.Property(entry => entry.Body)
            .IsRequired()
            .HasMaxLength(50000);

        builder
            .HasOne(entry => entry.Project)
            .WithMany()
            .HasForeignKey(entry => entry.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Task)
            .WithMany(task => task.Comments)
            .HasForeignKey(entry => entry.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Author)
            .WithMany()
            .HasForeignKey(entry => entry.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
