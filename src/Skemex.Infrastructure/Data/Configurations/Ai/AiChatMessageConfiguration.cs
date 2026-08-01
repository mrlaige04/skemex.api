using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("ai_chat_messages");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Content).HasMaxLength(8000).IsRequired();
        builder.Property(message => message.Role).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(message => new { message.ChatId, message.CreatedAt });
        builder.HasIndex(message => new { message.TenantId, message.ChatId });

        builder
            .HasOne(message => message.Chat)
            .WithMany(chat => chat.Messages)
            .HasForeignKey(message => message.ChatId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(message => message.DecompositionJob)
            .WithMany()
            .HasForeignKey(message => message.DecompositionJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(message => message.RootTask)
            .WithMany()
            .HasForeignKey(message => message.RootTaskId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
