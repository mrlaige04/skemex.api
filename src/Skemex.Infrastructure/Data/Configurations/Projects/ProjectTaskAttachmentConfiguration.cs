using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectTaskAttachmentConfiguration : IEntityTypeConfiguration<ProjectTaskAttachment>
{
    public void Configure(EntityTypeBuilder<ProjectTaskAttachment> builder)
    {
        builder.ToTable("project_task_attachments");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.FileName).HasMaxLength(256).IsRequired();
        builder.Property(entry => entry.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.BlobId).HasMaxLength(512).IsRequired();

        builder.HasIndex(entry => new { entry.TaskId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.ProjectId, entry.TaskId });
        builder.HasIndex(entry => new { entry.TenantId, entry.ProjectId });

        builder
            .HasOne(entry => entry.Project)
            .WithMany()
            .HasForeignKey(entry => entry.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Task)
            .WithMany(task => task.Attachments)
            .HasForeignKey(entry => entry.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.UploadedBy)
            .WithMany()
            .HasForeignKey(entry => entry.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
