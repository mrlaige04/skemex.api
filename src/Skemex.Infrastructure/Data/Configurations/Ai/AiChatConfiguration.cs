using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiChatConfiguration : IEntityTypeConfiguration<AiChat>
{
    public void Configure(EntityTypeBuilder<AiChat> builder)
    {
        builder.ToTable("ai_chats");
        builder.HasKey(chat => chat.Id);

        builder.Property(chat => chat.Title).HasMaxLength(120).IsRequired();

        builder.HasIndex(chat => new { chat.ProjectId, chat.CreatedByUserId, chat.UpdatedAt });
        builder.HasIndex(chat => new { chat.TenantId, chat.ProjectId });

        builder
            .HasOne(chat => chat.Project)
            .WithMany(project => project.AiChats)
            .HasForeignKey(chat => chat.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(chat => chat.CreatedByUser)
            .WithMany()
            .HasForeignKey(chat => chat.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
