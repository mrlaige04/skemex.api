using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectColumnConfiguration : IEntityTypeConfiguration<ProjectColumn>
{
    public void Configure(EntityTypeBuilder<ProjectColumn> builder)
    {
        builder.ToTable("project_columns");
        builder.HasKey(column => column.Id);

        builder.HasIndex(column => new { column.ProjectId, column.Key }).IsUnique();
        builder.HasIndex(column => new { column.ProjectId, column.SortOrder }).IsUnique();

        builder.Property(column => column.Key)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(column => column.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(column => column.Description)
            .HasMaxLength(2000);

        builder
            .HasOne(column => column.Project)
            .WithMany(project => project.Columns)
            .HasForeignKey(column => column.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
