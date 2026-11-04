using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Integrations;

namespace Skemex.Infrastructure.Data.Configurations.Integrations;

public sealed class IntegrationWebhookConfiguration : IEntityTypeConfiguration<IntegrationWebhook>
{
    public void Configure(EntityTypeBuilder<IntegrationWebhook> builder)
    {
        builder.ToTable("integration_webhooks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalWebhookId).HasMaxLength(64);
        builder.Property(x => x.Secret).HasMaxLength(128).IsRequired();
        builder.Property(x => x.SubscribedEventTypes).HasConversion<long>();
        builder.Property(x => x.EnabledEventTypes).HasConversion<long>();

        builder.HasIndex(x => x.Secret)
            .IsUnique()
            .HasFilter("\"Secret\" IS NOT NULL AND \"Secret\" <> ''");

        builder.HasIndex(x => x.ProjectIntegrationId);

        builder
            .HasOne(x => x.ProjectIntegration)
            .WithMany(i => i.Webhooks)
            .HasForeignKey(x => x.ProjectIntegrationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
