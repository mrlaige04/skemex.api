using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Projects;

namespace Skemex.Infrastructure.Data.Configurations.Projects;

public class ProjectSettingsConfiguration : IEntityTypeConfiguration<ProjectSettings>
{
    public void Configure(EntityTypeBuilder<ProjectSettings> builder)
    {
        builder.ToTable("project_settings");
        builder.HasKey(settings => settings.Id);
        builder.HasIndex(settings => settings.ProjectId).IsUnique();

        builder
            .HasOne(settings => settings.Project)
            .WithOne(project => project.Settings)
            .HasForeignKey<ProjectSettings>(settings => settings.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(settings => settings.DefaultTaskColumn)
            .WithMany()
            .HasForeignKey(settings => settings.DefaultTaskColumnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
