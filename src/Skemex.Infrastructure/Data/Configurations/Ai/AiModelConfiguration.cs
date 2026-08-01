using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiModelConfiguration : IEntityTypeConfiguration<AiModel>
{
    public void Configure(EntityTypeBuilder<AiModel> builder)
    {
        builder.ToTable("ai_models");
        builder.HasKey(model => model.Id);

        builder.Property(model => model.Provider).HasMaxLength(32).IsRequired();
        builder.Property(model => model.ExternalId).HasMaxLength(128).IsRequired();
        builder.Property(model => model.DisplayName).HasMaxLength(160).IsRequired();
        builder.Property(model => model.IconKey).HasMaxLength(32);

        builder.HasIndex(model => new { model.Provider, model.ExternalId }).IsUnique();
        builder.HasIndex(model => new { model.Provider, model.IsActive });
    }
}
