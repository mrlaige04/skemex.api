using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectDocumentChunkConfiguration : IEntityTypeConfiguration<ProjectDocumentChunk>
{
    public void Configure(EntityTypeBuilder<ProjectDocumentChunk> builder)
    {
        builder.ToTable("project_document_chunks");
        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Text).IsRequired();
        builder.Property(chunk => chunk.Embedding)
            .HasColumnType("vector(768)")
            .IsRequired();

        builder.HasIndex(chunk => chunk.ProjectId);
        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.ChunkIndex })
            .IsUnique();

        builder.HasIndex(chunk => chunk.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        builder
            .HasOne(chunk => chunk.Project)
            .WithMany()
            .HasForeignKey(chunk => chunk.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(chunk => chunk.Document)
            .WithMany()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
