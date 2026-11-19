using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Consts;
using Skemex.Domain.Entities.Users;

namespace Skemex.Infrastructure.Data.Configurations.Users;

public class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("tenant_settings");
        builder.HasKey(settings => settings.Id);
        builder.HasIndex(settings => settings.TenantId).IsUnique();

        builder
            .HasOne(settings => settings.Tenant)
            .WithOne(tenant => tenant.Settings)
            .HasForeignKey<TenantSettings>(settings => settings.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(settings => settings.AiSettings, ai =>
        {
            ai.Property(settings => settings.MaxTreeDepth)
                .HasDefaultValue(AiSettingsDefaults.MaxTreeDepth);

            ai.Property(settings => settings.MaxNodes)
                .HasDefaultValue(AiSettingsDefaults.MaxNodes);

            ai.Property(settings => settings.Temperature)
                .HasPrecision(3, 2)
                .HasDefaultValue(AiSamplingDefaults.Temperature);

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
