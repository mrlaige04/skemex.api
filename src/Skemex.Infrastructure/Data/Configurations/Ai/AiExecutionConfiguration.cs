using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiExecutionConfiguration : IEntityTypeConfiguration<AiExecution>
{
    public void Configure(EntityTypeBuilder<AiExecution> builder)
    {
        builder.ToTable("ai_executions");
        builder.HasKey(execution => execution.Id);

        builder.Property(execution => execution.Source)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(execution => execution.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(execution => execution.Title).HasMaxLength(256).IsRequired();
        builder.Property(execution => execution.ToolName).HasMaxLength(128);
        builder.Property(execution => execution.Model).HasMaxLength(256);
        builder.Property(execution => execution.UserInputPreview).HasMaxLength(500);
        builder.Property(execution => execution.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(execution => new { execution.TenantId, execution.CreatedAt });
        builder.HasIndex(execution => new { execution.TenantId, execution.StartedAt });
        builder.HasIndex(execution => new { execution.ProjectId, execution.StartedAt });
        builder.HasIndex(execution => new { execution.TenantId, execution.Source, execution.CreatedAt });
        builder.HasIndex(execution => execution.AiAgentJobId);
        builder.HasIndex(execution => execution.AiChatId);
        builder.HasIndex(execution => execution.ProjectDocumentId);

        builder
            .HasOne(execution => execution.User)
            .WithMany()
            .HasForeignKey(execution => execution.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(execution => execution.Project)
            .WithMany()
            .HasForeignKey(execution => execution.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(execution => execution.AiChat)
            .WithMany()
            .HasForeignKey(execution => execution.AiChatId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(execution => execution.AiAgentJob)
            .WithMany()
            .HasForeignKey(execution => execution.AiAgentJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(execution => execution.ProjectDocument)
            .WithMany()
            .HasForeignKey(execution => execution.ProjectDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}