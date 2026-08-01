using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiProviderConfiguration : IEntityTypeConfiguration<AiProvider>
{
    public void Configure(EntityTypeBuilder<AiProvider> builder)
    {
        builder.ToTable("ai_providers");
        builder.HasKey(provider => provider.Id);

        builder.Property(provider => provider.Name).HasMaxLength(120).IsRequired();
        builder.Property(provider => provider.Key).HasMaxLength(64).IsRequired();
        builder.Property(provider => provider.BaseUrl).HasMaxLength(512).IsRequired();
        builder.Property(provider => provider.ApiKeyEncrypted).HasMaxLength(4000);
        builder.Property(provider => provider.AuthConfigJson).HasColumnType("text").IsRequired();

        builder.HasIndex(provider => provider.Key).IsUnique();
        builder.HasIndex(provider => provider.IsEnabled);
    }
}
