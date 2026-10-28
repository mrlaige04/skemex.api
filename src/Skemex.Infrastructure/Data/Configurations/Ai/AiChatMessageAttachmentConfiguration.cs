using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiChatMessageAttachmentConfiguration : IEntityTypeConfiguration<AiChatMessageAttachment>
{
    public void Configure(EntityTypeBuilder<AiChatMessageAttachment> builder)
    {
        builder.ToTable("ai_chat_message_attachments");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.FileName).HasMaxLength(256).IsRequired();
        builder.Property(entry => entry.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.BlobId).HasMaxLength(512).IsRequired();

        builder.HasIndex(entry => new { entry.MessageId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.ChatId, entry.CreatedAt });
        builder.HasIndex(entry => new { entry.TenantId, entry.ChatId });

        builder
            .HasOne(entry => entry.Chat)
            .WithMany()
            .HasForeignKey(entry => entry.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(entry => entry.Message)
            .WithMany(message => message.Attachments)
            .HasForeignKey(entry => entry.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
