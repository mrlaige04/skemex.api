using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.ToTable("project_documents");
        builder.HasKey(document => document.Id);

        builder.Property(document => document.FileName).HasMaxLength(256).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(document => document.BlobId).HasMaxLength(512).IsRequired();

        builder.HasIndex(document => new { document.ProjectId, document.CreatedAt });
        builder.HasIndex(document => new { document.TenantId, document.ProjectId });

        builder
            .HasOne(document => document.Project)
            .WithMany(project => project.Documents)
            .HasForeignKey(document => document.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(document => document.UploadedBy)
            .WithMany()
            .HasForeignKey(document => document.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
