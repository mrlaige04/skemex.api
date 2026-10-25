using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiUsageActivityConfiguration : IEntityTypeConfiguration<AiUsageActivity>
{
    public void Configure(EntityTypeBuilder<AiUsageActivity> builder)
    {
        builder.ToTable("ai_usage_activities");
        builder.HasKey(activity => activity.Id);

        builder.Property(activity => activity.OperationType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(activity => activity.OperationName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(activity => activity.Provider).HasMaxLength(128);
        builder.Property(activity => activity.Model).HasMaxLength(256);
        builder.Property(activity => activity.ToolName).HasMaxLength(128);
        builder.Property(activity => activity.ErrorMessage).HasMaxLength(2000);
        builder.Property(activity => activity.MetadataJson).HasColumnType("text");

        // Dashboard / analytics indexes
        builder.HasIndex(activity => new { activity.TenantId, activity.CreatedAt });
        builder.HasIndex(activity => new { activity.TenantId, activity.UserId, activity.CreatedAt });
        builder.HasIndex(activity => new { activity.TenantId, activity.OperationType, activity.CreatedAt });
        builder.HasIndex(activity => new { activity.TenantId, activity.OperationName, activity.CreatedAt });
        builder.HasIndex(activity => new { activity.TenantId, activity.CorrelationId });
        builder.HasIndex(activity => new { activity.ProjectId, activity.CreatedAt });
        builder.HasIndex(activity => activity.AiAgentJobId);
        builder.HasIndex(activity => activity.AiChatId);
        builder.HasIndex(activity => activity.ProjectDocumentId);
        builder.HasIndex(activity => activity.ParentActivityId);
        builder.HasIndex(activity => activity.AiExecutionId);

        builder
            .HasOne(activity => activity.User)
            .WithMany()
            .HasForeignKey(activity => activity.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(activity => activity.Project)
            .WithMany()
            .HasForeignKey(activity => activity.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(activity => activity.AiChat)
            .WithMany()
            .HasForeignKey(activity => activity.AiChatId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(activity => activity.AiChatMessage)
            .WithMany()
            .HasForeignKey(activity => activity.AiChatMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(activity => activity.AiAgentJob)
            .WithMany()
            .HasForeignKey(activity => activity.AiAgentJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(activity => activity.ProjectDocument)
            .WithMany()
            .HasForeignKey(activity => activity.ProjectDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(activity => activity.AiExecution)
            .WithMany(execution => execution.Activities)
            .HasForeignKey(activity => activity.AiExecutionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
