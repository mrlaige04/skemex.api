using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Consts;
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

        builder.OwnsOne(settings => settings.AiSettings, ai =>
        {
            // Keep existing column names so this is a model-only refactor.
            ai.Property(settings => settings.MaxTreeDepth)
                .HasColumnName("AiMaxTreeDepth")
                .HasDefaultValue(AiSettingsDefaults.MaxTreeDepth);

            ai.Property(settings => settings.MaxNodes)
                .HasColumnName("AiMaxNodes")
                .HasDefaultValue(AiSettingsDefaults.MaxNodes);

            ai.Property(settings => settings.Temperature)
                .HasColumnName("AiTemperature")
                .HasPrecision(3, 2)
                .HasDefaultValue(AiSamplingDefaults.Temperature);

            ai.Property(settings => settings.DefaultModelId)
                .HasColumnName("DefaultAiModelId");

            ai.Property(settings => settings.EnrichmentModelId)
                .HasColumnName("EnrichmentAiModelId");

            ai.HasOne(settings => settings.DefaultModel)
                .WithMany()
                .HasForeignKey(settings => settings.DefaultModelId)
                .OnDelete(DeleteBehavior.SetNull);

            ai.HasOne(settings => settings.EnrichmentModel)
                .WithMany()
                .HasForeignKey(settings => settings.EnrichmentModelId)
                .OnDelete(DeleteBehavior.SetNull);

            ai.HasIndex(settings => settings.DefaultModelId);
            ai.HasIndex(settings => settings.EnrichmentModelId);
        });

        builder.Navigation(settings => settings.AiSettings).IsRequired();
    }
}
