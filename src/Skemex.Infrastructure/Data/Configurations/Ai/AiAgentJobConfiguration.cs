using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skemex.Domain.Entities.Ai;

namespace Skemex.Infrastructure.Data.Configurations.Ai;

public class AiAgentJobConfiguration : IEntityTypeConfiguration<AiAgentJob>
{
    public void Configure(EntityTypeBuilder<AiAgentJob> builder)
    {
        builder.ToTable("ai_agent_jobs");
        builder.HasKey(job => job.Id);

        builder.Property(job => job.ToolName).HasMaxLength(128);
        builder.Property(job => job.UserInput).HasMaxLength(8000).IsRequired();
        builder.Property(job => job.CustomInstructions).HasMaxLength(4000);
        builder.Property(job => job.ArgumentsJson).HasColumnType("text");
        builder.Property(job => job.Model).HasMaxLength(256);
        builder.Property(job => job.Error).HasMaxLength(4000);
        builder.Property(job => job.AssistantMessage).HasColumnType("text");
        builder.Property(job => job.ArtifactType).HasMaxLength(64);
        builder.Property(job => job.ArtifactPayloadJson).HasColumnType("text");
        builder.Property(job => job.HangfireJobId).HasMaxLength(64);
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(job => new { job.TenantId, job.CreatedAt });
        builder.HasIndex(job => new { job.ProjectId, job.CreatedAt });

        builder
            .HasOne(job => job.Project)
            .WithMany()
            .HasForeignKey(job => job.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(job => job.RequestedByUser)
            .WithMany()
            .HasForeignKey(job => job.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(job => job.AiChat)
            .WithMany()
            .HasForeignKey(job => job.AiChatId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
