using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.EmailTemplates;

namespace Skemex.Infrastructure.Data.Configurations.EmailTemplates;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("email_templates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(256).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Body).IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(t => new { t.Type, t.TenantId, t.IsSystem })
            .IsUnique()
            .HasFilter("\"IsSystem\" = true AND \"TenantId\" IS NULL");
    }
}
