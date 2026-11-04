using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Infrastructure.Data.Configurations.Integrations;

public sealed class ProjectIntegrationConfiguration : IEntityTypeConfiguration<ProjectIntegration>
{
    public void Configure(EntityTypeBuilder<ProjectIntegration> builder)
    {
        builder.ToTable("project_integrations");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TenantId, x.ProjectId, x.Provider }).IsUnique();

        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();

        builder.Property(x => x.CloudId).HasMaxLength(128);
        builder.Property(x => x.SiteUrl).HasMaxLength(512);
        builder.Property(x => x.SiteName).HasMaxLength(256);
        builder.Property(x => x.ExternalProjectId).HasMaxLength(128);
        builder.Property(x => x.ExternalProjectKey).HasMaxLength(64);
        builder.Property(x => x.ExternalProjectName).HasMaxLength(256);
        builder.Property(x => x.EncryptedAccessToken).HasMaxLength(4000);
        builder.Property(x => x.EncryptedRefreshToken).HasMaxLength(4000);
        builder.Property(x => x.Scopes).HasMaxLength(512);
        builder.Property(x => x.LastError).HasMaxLength(2000);

        builder
            .HasOne(x => x.Project)
            .WithMany(p => p.ProjectIntegrations)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
