using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Notifications;

namespace Skemex.Infrastructure.Data.Configurations.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Severity)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(notification => notification.Body)
            .HasMaxLength(2000);

        builder.Property(notification => notification.Href)
            .HasMaxLength(1024);

        builder.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
        builder.HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt });

        builder
            .HasOne(notification => notification.User)
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
